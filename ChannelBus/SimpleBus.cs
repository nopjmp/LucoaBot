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
        private readonly struct Request
        {
            public readonly object Message;
            public readonly CancellationToken CancellationToken;
            public readonly TaskCompletionSource<bool> CompletionSource;

            public Request(object message, CancellationToken cancellationToken,
                TaskCompletionSource<bool> completionSource)
            {
                Message = message;
                CancellationToken = cancellationToken;
                CompletionSource = completionSource;
            }
        }
        
        private readonly ConcurrentQueue<Subscription> _subscriptionRequests = new ConcurrentQueue<Subscription>();
        private readonly ConcurrentQueue<Guid> _unsubscribeRequests = new ConcurrentQueue<Guid>();
        private readonly Channel<Request> _channel = Channel.CreateUnbounded<Request>();
        private ChannelWriter<Request> ChannelWriter => _channel.Writer;
        private ChannelReader<Request> ChannelReader => _channel.Reader;

        public SimpleBus()
        {
            Task.Run(async () =>
            {
                var subscriptions = new List<Subscription>();
                
                while (await ChannelReader.WaitToReadAsync())
                {
                    while (_unsubscribeRequests.TryDequeue(out var subscriptionId))
                    {
                        var guid = subscriptionId;
                        subscriptions.RemoveAll(s => s.Guid == guid);
                    }

                    while (_subscriptionRequests.TryDequeue(out var subscription))
                    {
                        subscriptions.Add(subscription);
                    }
                    
                    while (ChannelReader.TryRead(out var request))
                    {
                        var result = true;
                        foreach (var subscription in subscriptions)
                        {
                            if (request.CancellationToken.IsCancellationRequested)
                            {
                                result = false;
                                break;
                            }

                            try
                            {
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
        
        public Task SendAsync<T>(T message) where T : struct
        {
            return SendAsync(message, CancellationToken.None);
        }

        public Task SendAsync<T>(T message, CancellationToken cancellationToken) where T : struct
        {
            var request = new Request(message, cancellationToken, new TaskCompletionSource<bool>());

            ChannelWriter.TryWrite(request);
            return request.CompletionSource.Task;
        }

        public Guid Subscribe<T>(Action<T> handler) where T : struct
        {
            return Subscribe<T>((message, cancellationToken) =>
            {
                handler.Invoke(message);
                return Task.CompletedTask;
            });
        }

        public Guid Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : struct
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