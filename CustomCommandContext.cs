using Discord.Commands;
using Discord.WebSocket;

namespace LucoaBot
{
    internal class CustomCommandContext : SocketCommandContext
    {
        public int ArgPos { get; set; }

        public CustomCommandContext(DiscordSocketClient client, SocketUserMessage msg) : base(client, msg)
        {
        }
    }
}