using System;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;

namespace LucoaBot.Models
{
    public class RawMessage
    {
        public ulong ChannelId { get; set; }
        public ulong MessageId { get; set; }

        private Optional<ISocketMessageChannel> _channel = Optional<ISocketMessageChannel>.Unspecified;
        private Optional<SocketUserMessage> _message = Optional<SocketUserMessage>.Unspecified;

        public ISocketMessageChannel GetChannel(DiscordSocketClient client)
        {
            if (!_channel.IsSpecified)
                _channel = Optional.Create(client.GetChannel(ChannelId) is ISocketMessageChannel messageChannel
                    ? messageChannel
                    : throw new ArgumentException("Could not find message channel."));

            return _channel.Value;
        }

        public async ValueTask<SocketUserMessage> GetMessage(DiscordSocketClient client)
        {
            var messageChannel = GetChannel(client);
            if (!_message.IsSpecified)
                _message = Optional.Create(
                    await messageChannel.GetMessageAsync(MessageId) is SocketUserMessage message
                        ? message
                        : throw new ArgumentException("Message ID was not user message."));

            return _message.Value;
        }
    }
}