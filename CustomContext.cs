using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LucoaBot.Models;

namespace LucoaBot
{
    public class CustomContext : ICommandContext
    {
        public DiscordSocketClient Client { get; private set; }

        public SocketGuild Guild { get; private set; }

        public ISocketMessageChannel Channel { get; private set; }

        public SocketUser User { get; private set; }

        public SocketUserMessage Message { get; private set; }

        public bool IsPrivate => Channel is IPrivateChannel;

        public int ArgPos { get; set; }

        IDiscordClient ICommandContext.Client => Client;
        IGuild ICommandContext.Guild => Guild;

        IMessageChannel ICommandContext.Channel => Channel;

        IUser ICommandContext.User => User;

        IUserMessage ICommandContext.Message => Message;

        public static async Task<CustomContext> Create(DiscordSocketClient client, RawMessage rawMessage)
        {
            var context = new CustomContext
            {
                Client = client,
                Channel = rawMessage.GetChannel(client),
                Message = await rawMessage.GetMessage(client)
            };

            context.Guild = context.Channel is SocketGuildChannel guildChannel ? guildChannel.Guild : null;
            context.User = context.Message.Author;

            return context;
        }
    }
}