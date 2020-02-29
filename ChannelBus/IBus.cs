using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paranoid.ChannelBus
{
    public interface IBus
    {
        Task SendAsync<T>(T message);
        Task SendAsync<T>(T message, CancellationToken cancellationToken);
        Guid Subscribe<T>(Action<T> handler);
        Guid Subscribe<T>(Func<T, CancellationToken, Task> handler);
        void Unsubscribe(Guid subscriptionId);
    }
}