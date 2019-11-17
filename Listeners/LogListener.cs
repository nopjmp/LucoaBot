using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LucoaBot.Listeners
{
    public class LogListener
    {
        private readonly DiscordSocketClient _client;
        private readonly DatabaseContext _context;

        public LogListener(ILogger<LogListener> logger, DatabaseContext context, DiscordSocketClient client)
        {
            _context = context;
            _client = client;
        }

        public void Initialize()
        {
            //client.MessageDeleted += Client_MessageDeleted;
            _client.UserJoined += Client_UserJoined;
            _client.UserLeft += Client_UserLeft;
        }

        private Task Client_UserJoined(SocketGuildUser user)
        {
            Task.Run(async () => { await UserMembershipUpdate(user, true); });

            return Task.CompletedTask;
        }

        private Task Client_UserLeft(SocketGuildUser user)
        {
            Task.Run(async () => { await UserMembershipUpdate(user, false); });

            return Task.CompletedTask;
        }

        private async Task UserMembershipUpdate(SocketGuildUser user, bool joined)
        {
            var config = await _context.GuildConfigs.AsQueryable()
                .Where(e => e.GuildId == user.Guild.Id)
                .FirstOrDefaultAsync();

            if (config.LogChannel.HasValue)
            {
                var channel = user.Guild.GetTextChannel(config.LogChannel.GetValueOrDefault());
                var update = joined ? "joined" : "left";
                if (channel != null)
                    await channel.SendMessageAsync(
                        $"{user.Username}#{user.Discriminator} with id `{user.Id}` has {update} the server.");
            }
        }
    }
}