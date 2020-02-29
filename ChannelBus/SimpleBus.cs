using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Channels;

namespace Paranoid.ChannelBus
{
    public class SimpleBus : IBus
    {
        private struct Request
        {
            public object Message;
            public CancellationToken CancellationToken;
            public TaskCompletionSource<bool> CompletionSource;
        }
        
        private readonly ConcurrentQueue<Subscription> _subscriptionRequests = new ConcurrentQueue<Subscription>();
        private readonly ConcurrentQueue<Guid> _unsubscribeRequests = new ConcurrentQueue<Guid>();
        private readonly ChannelWriter<Request> _channelWriter;

        public SimpleBus()
        {
            var channel = Channel.CreateUnbounded<Request>();
            var reader = channel.Reader;
            _channelWriter = channel.Writer;
            
            var subscriptions = new List<Subscription>();

            var _ = Task.Run(async () =>
            {
                while (await reader.WaitToReadAsync())
                {
                    while (_unsubscribeRequests.TryDequeue(out var guid))
                    {
                        Trace.TraceInformation($"Removing subscription {guid}");
                        
                        // ReSharper disable once AccessToModifiedClosure
                        subscriptions.RemoveAll(s => s.Guid == guid);
                    }

                    while (_subscriptionRequests.TryDequeue(out var subscription))
                    {
                        Trace.TraceInformation(($"Adding subscription {subscription.Guid}"));
                        subscriptions.Add(subscription);
                    }
                    
                    while (reader.TryRead(out var request))
                    {
                        var result = true;
                        foreach (var subscription in subscriptions)
                        {
                            if (request.CancellationToken.IsCancellationRequested)
                            {
                                Trace.TraceWarning("Cancellation requested. Processing stopped");
                                result = false;
                                break;
                            }

                            try
                            {
                                Trace.TraceInformation(($"Executing subscription {subscription.Guid}"));
                                await subscription.Handler.Invoke(request.Message, request.CancellationToken);
                            }
                            catch (Exception e)
                            {
                                Trace.TraceError("Exception while handling subscription {0}; Message: {1}", subscription.Guid, e.Message);
                                result = false;
                            }
                        }
                        request.CompletionSource.SetResult(result);
                    }
                }
            });
        }
        
        public Task SendAsync<T>(T message)
        {
            return SendAsync(message, CancellationToken.None);
        }

        public Task SendAsync<T>(T message, CancellationToken cancellationToken)
        {
            var request = new Request()
            {
                Message = message,
                CancellationToken = cancellationToken,
                CompletionSource = new TaskCompletionSource<bool>()
            };
            
            _channelWriter.TryWrite(request);
            return request.CompletionSource.Task;
        }

        public Guid Subscribe<T>(Action<T> handler)
        {
            return Subscribe<T>((message, cancellationToken) =>
            {
                handler.Invoke(message);
                return Task.CompletedTask;
            });
        }

        public Guid Subscribe<T>(Func<T, CancellationToken, Task> handler)
        {
            var subscription = Subscription.Create(handler);
            _subscriptionRequests.Enqueue(subscription);
            return subscription.Guid;
        }

        public void Unsubscribe(Guid subscriptionId)
        {
            _unsubscribeRequests.Enqueue(subscriptionId);
        }
    }
}