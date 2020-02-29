using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using LucoaBot.Models;
using Paranoid.ChannelBus;

namespace LucoaBot.Services
{
    public class BusQueue
    {
        private readonly DiscordSocketClient _client;
        private readonly IBus _bus;

        private Guid _messageReceivedGuid;
        private Guid _messageDeletedGuid;
        private Guid _userActionGuid;
        private Guid _logGuid;
        private Guid _reactionGuid;

        public BusQueue(DiscordSocketClient client)
        {
            _client = client;
            _bus = new SimpleBus();
        }

        public void Start()
        {
            _client.MessageReceived += OnMessageReceived;
            _client.MessageDeleted += OnMessageDeleted;
            _client.UserJoined += OnUserJoined;
            _client.UserLeft += OnUserLeft;
            _client.ReactionAdded += OnReactionAdded;
            _client.ReactionRemoved += OnReactionRemoved;
            _client.ReactionsCleared += OnReactionsCleared;

            _messageReceivedGuid = _bus.Subscribe<RawMessage>(OnMessageReceived);
            _messageDeletedGuid = _bus.Subscribe<RawMessage>(OnMessageDeleted);
            _userActionGuid = _bus.Subscribe<UserActionMessage>(OnUserAction);
            _logGuid = _bus.Subscribe<EventLogMessage>(OnLog);
            _reactionGuid = _bus.Subscribe<ReactionMessage>(OnReaction);
        }

        public void Stop()
        {
            _bus.Unsubscribe(_messageReceivedGuid);
            _bus.Unsubscribe(_messageDeletedGuid);
            _bus.Unsubscribe(_userActionGuid);
            _bus.Unsubscribe(_logGuid);
            _bus.Unsubscribe(_reactionGuid);
        }

        #region MessageReceived Handler

        private readonly List<Func<CustomContext, Task>> _messageReceivedEvent =
            new List<Func<CustomContext, Task>>();

        // ValueTuple of channel, message
        public event Func<CustomContext, Task> MessageReceived
        {
            add
            {
                lock (_messageReceivedEvent) _messageReceivedEvent.Add(value);
            }
            remove
            {
                lock (_messageReceivedEvent) _messageReceivedEvent.Remove(value);
            }
        }

        private Task OnMessageReceived(SocketMessage socketUserMessage)
        {
            if (!socketUserMessage.Author.IsBot)
            {
                Task.Run(async () =>
                {
                    await _bus.SendAsync(new RawMessage()
                    {
                        IsDeleted = false,
                        ChannelId = socketUserMessage.Channel.Id,
                        MessageId = socketUserMessage.Id
                    });
                }).SafeFireAndForget(false);
            }

            return Task.CompletedTask;
        }

        private async Task OnMessageReceived(RawMessage message, CancellationToken cancellationToken)
        {
            if (message.IsDeleted) return;

            var context = await CustomContext.Create(_client, message);
            var tasks = new List<Task>();
            lock (_messageReceivedEvent)
            {
                tasks.AddRange(_messageReceivedEvent.Select(func =>
                {
                    return Task.Run(async () => await func(context), cancellationToken);
                }));
            }

            await Task.WhenAll(tasks);
        }

        #endregion

        #region MessageDeleted Handler

        private readonly List<Func<RawMessage, Task>> _messageDeletedEvent =
            new List<Func<RawMessage, Task>>();

        public event Func<RawMessage, Task> MessageDeleted
        {
            add
            {
                lock (_messageDeletedEvent) _messageDeletedEvent.Add(value);
            }
            remove
            {
                lock (_messageDeletedEvent) _messageDeletedEvent.Remove(value);
            }
        }

        private Task OnMessageDeleted(Cacheable<IMessage, ulong> cacheMessage, ISocketMessageChannel messageChannel)
        {
            Task.Run(async () =>
            {
                await _bus.SendAsync(new RawMessage()
                {
                    IsDeleted = true,
                    ChannelId = messageChannel.Id,
                    MessageId = cacheMessage.Id
                });
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }

        private async Task OnMessageDeleted(RawMessage message, CancellationToken cancellationToken)
        {
            if (!message.IsDeleted) return;

            var tasks = new List<Task>();
            lock (_messageDeletedEvent)
            {
                tasks.AddRange(_messageDeletedEvent.Select(func =>
                {
                    return Task.Run(async () => await func(message), cancellationToken);
                }));
            }

            await Task.WhenAll(tasks);
        }

        #endregion

        #region User Action Handler

        private readonly List<Func<UserActionMessage, Task>> _userActionEvent =
            new List<Func<UserActionMessage, Task>>();

        public event Func<UserActionMessage, Task> UserActionEvent
        {
            add
            {
                lock (_userActionEvent) _userActionEvent.Add(value);
            }
            remove
            {
                lock (_userActionEvent) _userActionEvent.Remove(value);
            }
        }

        private Task OnUserJoined(SocketGuildUser user)
        {
            Task.Run(async () =>
            {
                await _bus.SendAsync(new UserActionMessage()
                {
                    UserAction = UserAction.Join,
                    Id = user.Id,
                    GuildId = user.Guild.Id,
                    Username = user.Username,
                    Discriminator = user.Discriminator
                });
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }

        private Task OnUserLeft(SocketGuildUser user)
        {
            Task.Run(async () =>
            {
                await _bus.SendAsync(new UserActionMessage()
                {
                    UserAction = UserAction.Left,
                    Id = user.Id,
                    GuildId = user.Guild.Id,
                    Username = user.Username,
                    Discriminator = user.Discriminator
                });
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
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
                lock (_eventLogEvent) _eventLogEvent.Add(value);
            }
            remove
            {
                lock (_eventLogEvent) _eventLogEvent.Remove(value);
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

        public async Task SubmitLog(IUser user, IGuild guild, string message, string actionTaken)
        {
            if (guild == null) return; // skip message

            await _bus.SendAsync(new EventLogMessage()
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

        #region Reaction Handling

        private readonly List<Func<ReactionMessage, Task>> _reactionEvent =
            new List<Func<ReactionMessage, Task>>();

        public event Func<ReactionMessage, Task> ReactionEvent
        {
            add
            {
                lock (_reactionEvent) _reactionEvent.Add(value);
            }
            remove
            {
                lock (_reactionEvent) _reactionEvent.Remove(value);
            }
        }

        private Task OnReactionAdded(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel,
            SocketReaction reaction)
        {
            Task.Run(async () =>
            {
                if (channel is SocketGuildChannel guildChannel)
                {
                    await _bus.SendAsync(new ReactionMessage()
                    {
                        ReactionAction = ReactionAction.Added,
                        MessageId = message.Id,
                        ChannelId = channel.Id,
                        GuildId = guildChannel.Guild.Id,
                        Emote = reaction.Emote.Name
                    });
                }
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }

        private Task OnReactionRemoved(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel,
            SocketReaction reaction)
        {
            Task.Run(async () =>
            {
                if (channel is SocketGuildChannel guildChannel)
                {
                    await _bus.SendAsync(new ReactionMessage()
                    {
                        ReactionAction = ReactionAction.Removed,
                        MessageId = message.Id,
                        ChannelId = channel.Id,
                        GuildId = guildChannel.Guild.Id,
                        Emote = reaction.Emote.Name
                    });
                }
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }

        private Task OnReactionsCleared(Cacheable<IUserMessage, ulong> message, ISocketMessageChannel channel)
        {
            Task.Run(async () =>
            {
                if (channel is SocketGuildChannel guildChannel)
                {
                    await _bus.SendAsync(new ReactionMessage()
                    {
                        ReactionAction = ReactionAction.Removed,
                        MessageId = message.Id,
                        ChannelId = channel.Id,
                        GuildId = guildChannel.Guild.Id,
                        Emote = ""
                    });
                }
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }

        private async Task OnReaction(ReactionMessage message, CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            lock (_reactionEvent)
            {
                tasks.AddRange(_reactionEvent.Select(func =>
                {
                    return Task.Run(async () => await func(message), cancellationToken);
                }));
            }

            await Task.WhenAll(tasks);
        }

        #endregion
    }
}