using System;
using System.Threading;
using System.Threading.Tasks;

namespace Paranoid.ChannelBus
{
    internal sealed class Subscription
    {
        public Guid Guid { get; }
        public Func<object, CancellationToken, Task> Handler { get; }

        private Subscription(Func<object, CancellationToken, Task> handler)
        {
            Guid = Guid.NewGuid();
            Handler = handler;
        }

        public static Subscription Create<TMessage>(Func<TMessage, CancellationToken, Task> handler)
        {
            async Task HandlerWithCheck(object o, CancellationToken token)
            {
                if (o is TMessage message)
                {
                    await handler.Invoke(message, token);
                }
            }

            return new Subscription(HandlerWithCheck);
        }
    }
}