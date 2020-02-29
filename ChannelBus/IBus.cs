using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paranoid.ChannelBus
{
    public interface IBus
    {
        Task SendAsync<T>(T message) where T : struct;
        Task SendAsync<T>(T message, CancellationToken cancellationToken) where T : struct;
        Guid Subscribe<T>(Action<T> handler) where T : struct;
        Guid Subscribe<T>(Func<T, CancellationToken, Task> handler) where T : struct;
        void Unsubscribe(Guid subscriptionId);
    }
}