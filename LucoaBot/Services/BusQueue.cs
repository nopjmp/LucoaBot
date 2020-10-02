using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using LucoaBot.Models;
using Paranoid.ChannelBus;

namespace LucoaBot.Services
{
    public class BusQueue
    {
        private readonly IBus _bus;
        private readonly DiscordClient _client;
        private Guid _logGuid;

        private Guid _userActionGuid;

        public BusQueue(DiscordClient client)
        {
            _client = client;
            _bus = new SimpleBus();

            _client.GuildMemberAdded += (_, e) =>
            {
                OnUserJoined(e).Forget();
                return Task.CompletedTask;
            };
            _client.GuildMemberRemoved += (_, e) =>
            {
                OnUserLeft(e).Forget();
                return Task.CompletedTask;
            };

            _userActionGuid = _bus.Subscribe<UserActionMessage>(OnUserAction);
            _logGuid = _bus.Subscribe<EventLogMessage>(OnLog);
        }

        #region User Action Handler

        private readonly List<Func<UserActionMessage, Task>> _userActionEvent =
            new List<Func<UserActionMessage, Task>>();

        public event Func<UserActionMessage, Task> UserActionEvent
        {
            add
            {
                lock (_userActionEvent)
                {
                    _userActionEvent.Add(value);
                }
            }
            remove
            {
                lock (_userActionEvent)
                {
                    _userActionEvent.Remove(value);
                }
            }
        }

        private async Task OnUserJoined(GuildMemberAddEventArgs args)
        {
            await _bus.SendAsync(new UserActionMessage
            {
                UserAction = UserAction.Join,
                Id = args.Member.Id,
                GuildId = args.Guild.Id,
                Username = args.Member.Username,
                Discriminator = args.Member.Discriminator
            });
        }

        private async Task OnUserLeft(GuildMemberRemoveEventArgs args)
        {
            await _bus.SendAsync(new UserActionMessage
            {
                UserAction = UserAction.Left,
                Id = args.Member.Id,
                GuildId = args.Guild.Id,
                Username = args.Member.Username,
                Discriminator = args.Member.Discriminator
            });
        }

        private async Task OnUserAction(UserActionMessage message, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            lock (_userActionEvent)
            {
                tasks.AddRange(_userActionEvent.Select(func =>
                {
                    return Task.Run(async () => await func(message), cancellationToken);
                }));
            }

            await Task.WhenAll(tasks);
        }

        #endregion

        #region Log Handler

        private readonly List<Func<EventLogMessage, Task>> _eventLogEvent =
            new List<Func<EventLogMessage, Task>>();

        public event Func<EventLogMessage, Task> EventLogEvent
        {
            add
            {
                lock (_eventLogEvent)
                {
                    _eventLogEvent.Add(value);
                }
            }
            remove
            {
                lock (_eventLogEvent)
                {
                    _eventLogEvent.Remove(value);
                }
            }
        }

        private async Task OnLog(EventLogMessage message, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            lock (_eventLogEvent)
            {
                tasks.AddRange(_eventLogEvent.Select(func =>
                {
                    return Task.Run(async () => await func(message), cancellationToken);
                }));
            }

            await Task.WhenAll(tasks);
        }

        public async Task SubmitLog(DiscordUser user, DiscordGuild guild, string message, string actionTaken)
        {
            if (guild == null) return; // skip message

            await _bus.SendAsync(new EventLogMessage
            {
                Id = user.Id,
                GuildId = guild.Id,
                Username = user.Username,
                Discriminator = user.Discriminator,
                Message = message,
                ActionTaken = actionTaken
            });
        }

        #endregion
    }
}