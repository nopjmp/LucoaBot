using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using LucoaBot.Data;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LucoaBot.Listeners
{
    public class LogListener
    {
        private readonly BusQueue _busQueue;
        private readonly DiscordClient _client;
        private readonly DatabaseContext _context;

        public LogListener(DatabaseContext context, DiscordClient client,
            BusQueue busQueue)
        {
            _context = context;
            _client = client;
            _busQueue = busQueue;
        }

        public void Start()
        {
            //client.MessageDeleted += Client_MessageDeleted;
            _busQueue.UserActionEvent += OnUserActionEvent;
            _busQueue.EventLogEvent += OnEventLogEvent;
        }

        public void Stop()
        {
            //client.MessageDeleted -= Client_MessageDeleted;
            _busQueue.UserActionEvent -= OnUserActionEvent;
            _busQueue.EventLogEvent -= OnEventLogEvent;
        }

        private async Task OnUserActionEvent(UserActionMessage userActionMessage)
        {
            var config = await _context.GuildConfigs.AsNoTracking()
                .Where(e => e.GuildId == userActionMessage.GuildId)
                .FirstOrDefaultAsync();

            if (config.LogChannel.HasValue && _client.Guilds.TryGetValue(userActionMessage.GuildId, out var guild))
            {
                var channel = guild.GetChannel(config.LogChannel.GetValueOrDefault());
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

            if (config.LogChannel.HasValue && _client.Guilds.TryGetValue(eventLogMessage.GuildId, out var guild))
            {
                var channel = guild.GetChannel(config.LogChannel.GetValueOrDefault());

                if (channel != null)
                    await channel.SendMessageAsync(
                        $"{eventLogMessage.Username}#{eventLogMessage.Discriminator} with id `{eventLogMessage.Id}`" +
                        $" {eventLogMessage.Message} `action taken: {eventLogMessage.ActionTaken}`");
            }
        }
    }
}