using Discord.Commands;
using Discord.WebSocket;

namespace LucoaBot
{
    internal class CustomCommandContext : SocketCommandContext
    {
        public CustomCommandContext(DiscordSocketClient client, SocketUserMessage msg) : base(client, msg)
        {
        }

        public int ArgPos { get; set; }
    }
}