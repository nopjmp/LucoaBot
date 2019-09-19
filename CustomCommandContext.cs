using Discord.Commands;
using Discord.WebSocket;
using LucoaBot.Models;

namespace LucoaBot
{
    internal class CustomCommandContext : SocketCommandContext
    {
        public GuildConfig Config { get; set; }
        public int ArgPos { get; set; }
        public CustomCommandContext(DiscordSocketClient client, SocketUserMessage msg) : base(client, msg)
        {
        }
    }
}
