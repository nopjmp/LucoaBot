using System;
using System.Threading;
using System.Threading.Tasks;

namespace LucoaBot.Utils
{
    internal sealed class Subscription
    {
        private Subscription(Func<object, CancellationToken, Task> handler)
        {
            Guid = Guid.NewGuid();
            Handler = handler;
        }

        public Guid Guid { get; }
        public Func<object, CancellationToken, Task> Handler { get; }

        public static Subscription Create<TMessage>(Func<TMessage, CancellationToken, Task> handler)
        {
            async Task HandlerWithCheck(object o, CancellationToken token)
            {
                if (o is TMessage message) await handler.Invoke(message, token);
            }

            return new Subscription(HandlerWithCheck);
        }
    }
}