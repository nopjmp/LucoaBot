using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using LucoaBot.Models;
using MsgPack.Serialization;
using StackExchange.Redis;
using LogMessage = Discord.LogMessage;

namespace LucoaBot.Services
{
    public class RedisQueue
    {
        private readonly DiscordSocketClient _client;
        private readonly ISubscriber _subscriber;

        private const string LucoaMessagesChannel = "lucoa:messages";
        private const string LucoaDeletedChannel = "lucoa:deleted";
        private const string LucoaUserActionChannel = "lucoa:user:action";
        private const string LucoaEventLogChannel = "lucoa:eventlog";

        public RedisQueue(DiscordSocketClient client, IConnectionMultiplexer connection)
        {
            _client = client;
            _subscriber = connection.GetSubscriber();
        }

        public void Start()
        {
            _client.MessageReceived += OnMessageReceived;
            _client.MessageDeleted += OnMessageDeleted;
            _client.UserJoined += OnUserJoined;
            _client.UserLeft += OnUserLeft;

            _subscriber.Subscribe(LucoaMessagesChannel).OnMessage(OnMessageReceived);
            _subscriber.Subscribe(LucoaDeletedChannel).OnMessage(OnMessageDeleted);
            _subscriber.Subscribe(LucoaUserActionChannel).OnMessage(OnUserAction);
            _subscriber.Subscribe(LucoaEventLogChannel).OnMessage(OnLog);
        }

        public void Stop()
        {
            _subscriber.UnsubscribeAll();
        }

        #region MessageReceived Handler

        private readonly MessagePackSerializer<RawMessage> _messageReceivedSerializer =
            MessagePackSerializer.Get<RawMessage>();

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
                    var msg = _messageReceivedSerializer.PackSingleObject(new RawMessage()
                    {
                        ChannelId = socketUserMessage.Channel.Id,
                        MessageId = socketUserMessage.Id
                    });
                    await _subscriber.PublishAsync(LucoaMessagesChannel, msg);
                }).SafeFireAndForget(false);
            }

            return Task.CompletedTask;
        }

        private async Task OnMessageReceived(ChannelMessage message)
        {
            if (message.Message.IsNullOrEmpty) return;

            var rawMessage = _messageReceivedSerializer.UnpackSingleObject(message.Message);
            var context = await CustomContext.Create(_client, rawMessage);
            var tasks = new List<Task>();
            lock (_messageReceivedEvent)
            {
                tasks.AddRange(_messageReceivedEvent.Select(func => Task.Run(async () => await func(context))));
            }

            await Task.WhenAll(tasks);
        }

        #endregion

        #region MessageDeleted Handler

        private readonly MessagePackSerializer<RawMessage> _messageDeletedSerializer =
            MessagePackSerializer.Get<RawMessage>();

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
                var msg = _messageDeletedSerializer.PackSingleObject(new RawMessage()
                {
                    ChannelId = messageChannel.Id,
                    MessageId = cacheMessage.Id
                });
                await _subscriber.PublishAsync(LucoaDeletedChannel, msg);
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }

        private async Task OnMessageDeleted(ChannelMessage message)
        {
            if (message.Message.IsNullOrEmpty) return;

            var rawMessage = _messageReceivedSerializer.UnpackSingleObject(message.Message);
            var tasks = new List<Task>();
            lock (_messageDeletedEvent)
            {
                tasks.AddRange(_messageDeletedEvent.Select(func => Task.Run(async () => await func(rawMessage))));
            }

            await Task.WhenAll(tasks);
        }

        #endregion

        #region User Action Handler

        private readonly MessagePackSerializer<UserActionMessage> _userActionSerializer =
            MessagePackSerializer.Get<UserActionMessage>();

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
                var msg = _userActionSerializer.PackSingleObject(new UserActionMessage()
                {
                    UserAction = UserAction.Join,
                    Id = user.Id,
                    GuildId = user.Guild.Id,
                    Username = user.Username,
                    Discriminator = user.Discriminator
                });
                await _subscriber.PublishAsync(LucoaUserActionChannel, msg);
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }

        private Task OnUserLeft(SocketGuildUser user)
        {
            Task.Run(async () =>
            {
                var msg = _userActionSerializer.PackSingleObject(new UserActionMessage()
                {
                    UserAction = UserAction.Left,
                    Id = user.Id,
                    GuildId = user.Guild.Id,
                    Username = user.Username,
                    Discriminator = user.Discriminator
                });
                await _subscriber.PublishAsync(LucoaUserActionChannel, msg);
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }

        private async Task OnUserAction(ChannelMessage message)
        {
            if (message.Message.IsNullOrEmpty) return;

            var rawMessage = _userActionSerializer.UnpackSingleObject(message.Message);
            var tasks = new List<Task>();
            lock (_userActionEvent)
            {
                tasks.AddRange(_userActionEvent.Select(func => Task.Run(async () => await func(rawMessage))));
            }

            await Task.WhenAll(tasks);
        }

        #endregion

        #region Log Handler

        private readonly MessagePackSerializer<EventLogMessage> _eventLogSerializer =
            MessagePackSerializer.Get<EventLogMessage>();

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

        private async Task OnLog(ChannelMessage message)
        {
            if (message.Message.IsNullOrEmpty) return;

            var rawMessage = _eventLogSerializer.UnpackSingleObject(message.Message);
            var tasks = new List<Task>();
            lock (_eventLogEvent)
            {
                tasks.AddRange(_eventLogEvent.Select(func => Task.Run(async () => await func(rawMessage))));
            }

            await Task.WhenAll(tasks);
        }

        public async Task SubmitLog(IUser user, IGuild guild, string message, string actionTaken)
        {
            if (guild == null) return; // skip message
                var msg = _eventLogSerializer.PackSingleObject(new EventLogMessage()
            {
                Id = user.Id,
                GuildId = guild.Id,
                Username = user.Username,
                Discriminator = user.Discriminator,
                Message = message,
                ActionTaken = actionTaken
            });
            await _subscriber.PublishAsync(LucoaEventLogChannel, msg);
        }

    #endregion
    }
}