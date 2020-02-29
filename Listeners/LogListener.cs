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
        private readonly SimpleBus _bus;

        public LogListener(ILogger<LogListener> logger, DatabaseContext context, DiscordSocketClient client,
            SimpleBus bus)
        {
            _context = context;
            _client = client;
            _bus = bus;
        }

        public void Initialize()
        {
            //client.MessageDeleted += Client_MessageDeleted;
            _bus.UserActionEvent += OnUserActionEvent;
            _bus.EventLogEvent += OnEventLogEvent;
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

        private async Task OnEventLogEvent(EventLogMessage eventLogMessage)
        {
            var config = await _context.GuildConfigs.AsNoTracking()
                .Where(e => e.GuildId == eventLogMessage.GuildId)
                .FirstOrDefaultAsync();

            if (config.LogChannel.HasValue)
            {
                var guild = _client.GetGuild(eventLogMessage.GuildId);
                var channel = guild.GetTextChannel(config.LogChannel.GetValueOrDefault());

                if (channel != null)
                    await channel.SendMessageAsync(
                        $"{eventLogMessage.Username}#{eventLogMessage.Discriminator} with id `{eventLogMessage.Id}`" +
                        $" {eventLogMessage.Message} `action taken: {eventLogMessage.ActionTaken}`");
            }
        }
    }
}