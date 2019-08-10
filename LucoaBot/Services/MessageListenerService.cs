using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace LucoaBot.Services
{
    internal class MessageListenerService
    {
        private readonly IServiceProvider services;
        private readonly DiscordSocketClient client;
        private readonly DatabaseContext context;
        private readonly Assembly assembly;

        public MessageListenerService(IServiceProvider services, DiscordSocketClient client, DatabaseContext context)
        {
            this.services = services;
            this.client = client;
            this.context = context;
            this.assembly = Assembly.GetEntryAssembly();
        }

        public void Initialize()
        {
            foreach (var typeInfo in assembly.DefinedTypes)
            {

            }
        }
    }
}
