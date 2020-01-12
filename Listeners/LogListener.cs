using System.Linq;
using System.Threading.Tasks;
using Discord.WebSocket;
using LucoaBot.Models;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LucoaBot.Listeners
{
    public class LogListener
    {
        private readonly DiscordSocketClient _client;
        private readonly DatabaseContext _context;
        private readonly RedisQueue _redisQueue;

        public LogListener(ILogger<LogListener> logger, DatabaseContext context, DiscordSocketClient client,
            RedisQueue redisQueue)
        {
            _context = context;
            _client = client;
            _redisQueue = redisQueue;
        }

        public void Initialize()
        {
            //client.MessageDeleted += Client_MessageDeleted;
            _redisQueue.UserActionEvent += OnUserActionEvent;
        }

        private async Task OnUserActionEvent(UserActionMessage userActionMessage)
        {
            var config = await _context.GuildConfigs.AsNoTracking()
                .Where(e => e.GuildId == userActionMessage.GuildId)
                .FirstOrDefaultAsync();

            if (config.LogChannel.HasValue)
            {
                var guild = _client.GetGuild(userActionMessage.GuildId);
                var channel = guild.GetTextChannel(config.LogChannel.GetValueOrDefault());

                if (channel != null)
                    await channel.SendMessageAsync(
                        $"{userActionMessage.Username}#{userActionMessage.Discriminator} with id `{userActionMessage.Id}` has " +
                        $"{userActionMessage.UserAction.ToFriendly()} the server.");
            }
        }
    }
}