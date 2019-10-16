using Discord.WebSocket;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Threading.Tasks;

namespace LucoaBot.Listeners
{
    public class LogListener
    {
        private readonly ILogger<LogListener> logger;
        private readonly DatabaseContext context;
        private readonly DiscordSocketClient client;

        public LogListener(ILogger<LogListener> logger, DatabaseContext context, DiscordSocketClient client)
        {
            this.logger = logger;
            this.context = context;
            this.client = client;
        }

        public void Initialize()
        {
            //client.MessageDeleted += Client_MessageDeleted;
            client.UserJoined += Client_UserJoined;
            client.UserLeft += Client_UserLeft;
        }

        private Task Client_UserJoined(SocketGuildUser user)
        {
            Task.Run(async () =>
            {
                await UserMembershipUpdate(user, true);
            });

            return Task.CompletedTask;
        }

        private Task Client_UserLeft(SocketGuildUser user)
        {
            Task.Run(async () =>
            {
                await UserMembershipUpdate(user, false);
            });

            return Task.CompletedTask;
        }

        private async Task UserMembershipUpdate(SocketGuildUser user, bool joined)
        {
            var config = await context.GuildConfigs
                .Where(e => e.GuildId == user.Guild.Id)
                .FirstOrDefaultAsync();

            if (config.LogChannel.HasValue)
            {
                var channel = user.Guild.GetTextChannel(config.LogChannel.Value);
                var update = joined ? "joined" : "left";
                await channel?.SendMessageAsync($"{user.Username}#{user.Discriminator} with id `{user.Id}` has {update} the server.");
            }
        }
    }
}
