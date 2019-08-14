using Discord;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LucoaBot.Listeners
{
    class StarboardListener
    {
        private readonly DatabaseContext context;
        private readonly DiscordSocketClient client;

        private readonly Emoji emoji = new Emoji("⭐");
#if !DEBUG
        private readonly static int DEFAULT_THRESHOLD = 3;
#else
        private readonly static int DEFAULT_THRESHOLD = 1;
#endif
        public StarboardListener(DiscordSocketClient client, DatabaseContext context)
        {
            this.client = client;
            this.context = context;
        }

        public void Initialize()
        {
            client.ReactionAdded += Client_ReactionAdded;
            client.ReactionRemoved += Client_ReactionRemoved;
            client.ReactionsCleared += Client_ReactionsCleared;
        }

        private Task<IMessage> FindStarPost(SocketTextChannel starboardChannel, SocketTextChannel channel, IUserMessage message)
        {
            var messageId = message.Id.ToString();
            var dateThreshold = DateTimeOffset.Now.AddDays(-1);
            var messages = starboardChannel.GetMessagesAsync().Flatten();
            return (from m in messages
                    where m.Author.Id == client.CurrentUser.Id
                         && m.CreatedAt > dateThreshold
                         && m.Embeds.SelectMany(e => e.Fields).Any(f => f.Name == "Message ID" && f.Value == messageId)
                    select m).FirstOrDefault();
        }

        private async Task ProcessReaction(Models.GuildConfig config, SocketTextChannel channel, IUserMessage message, int count)
        {
            // config should always have a value here
            var starboardChannel = channel.Guild.GetTextChannel(config.StarBoardChannel.Value);
            if (starboardChannel != null)
            {
                var starboardMessage = await FindStarPost(starboardChannel, channel, message) as IUserMessage;

                if (count < DEFAULT_THRESHOLD)
                {
                    if (starboardMessage != null)
                    {
                        await starboardMessage.DeleteAsync();
                    }
                }
                else
                {
                    var scale = 255 - Math.Clamp((count - DEFAULT_THRESHOLD) * 25, 0, 255);

                    var embedBuilder = new EmbedBuilder()
                    {
                        Title = $"{emoji} **{count}**",
                        Color = new Color(255, 255, scale),
                        Author = new EmbedAuthorBuilder()
                        {
                            Name = $"{message.Author.Username}#{message.Author.Discriminator}",
                            IconUrl = message.Author.GetAvatarUrl()
                        },
                        Description = message.Content,
                        Timestamp = message.CreatedAt
                    };

                    if (message.Attachments.Any())
                    {
                        var attachment = message.Attachments.First();
                        if (attachment.Url != null)
                        {
                            if (attachment.IsSpoiler())
                            {
                                embedBuilder.AddField("SPOILER", attachment.Url);
                            }
                            else
                            {
                                embedBuilder.ImageUrl = attachment.Url;
                            }
                        }
                    }
                    else if (message.Embeds.Any())
                    {
                        var embed = message.Embeds.First();
                        switch (embed.Type)
                        {
                            case EmbedType.Image:
                                embedBuilder.ImageUrl = embed.Image.Value.Url;
                                break;
                                //case EmbedType.Gifv
                        }
                    }
                    embedBuilder.AddField("Channel", channel.Mention, true)
                                .AddField("Message ID", message.Id, true)
                                .AddField("Link to message", $"[Click here to go to the original message.]({message.GetJumpUrl()})", false);

                    if (starboardMessage == null)
                    {
                        await starboardChannel.SendMessageAsync(embed: embedBuilder.Build());
                    }
                    else
                    {
                        await starboardMessage.ModifyAsync(p => p.Embed = embedBuilder.Build());
                    }
                }
            }
        }

        private Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> _message, ISocketMessageChannel _channel, SocketReaction reaction)
        {
            if (reaction.Emote.Name == emoji.Name
                && _channel is SocketTextChannel)
            {
                var _ = Task.Run(async () =>
                {
                    var channel = _channel as SocketTextChannel;
                    var config = await context.GuildConfigs
                        .Where(e => e.GuildId == channel.Guild.Id)
                        .FirstOrDefaultAsync();

                    // only process if the starboard channel has a value and it's not in the starboard.
                    if (config.StarBoardChannel.HasValue && channel.Id != config.StarBoardChannel)
                    {
                        var message = await _message.GetOrDownloadAsync();

                        ReactionMetadata reactionMetadata;
                        if (message.Reactions.TryGetValue(emoji, out reactionMetadata))
                        {
                            await ProcessReaction(config, channel, message, reactionMetadata.ReactionCount);
                        }
                    }
                });
            }

            return Task.CompletedTask;
        }

        private Task Client_ReactionRemoved(Cacheable<IUserMessage, ulong> _message, ISocketMessageChannel _channel, SocketReaction reaction)
        {
            if (reaction.Emote.Name == emoji.Name
                && _channel is SocketTextChannel)
            {
                var _ = Task.Run(async () =>
                {
                    var channel = _channel as SocketTextChannel;
                    var config = await context.GuildConfigs
                        .Where(e => e.GuildId == channel.Guild.Id)
                        .FirstOrDefaultAsync();

                    // only process if the starboard channel has a value and it's not in the starboard.
                    if (config.StarBoardChannel.HasValue && channel.Id != config.StarBoardChannel)
                    {
                        var message = await _message.GetOrDownloadAsync();

                        var count = 0;
                        ReactionMetadata reactionMetadata;
                        if (message.Reactions.TryGetValue(emoji, out reactionMetadata))
                        {
                            count = reactionMetadata.ReactionCount;
                        }

                        await ProcessReaction(config, channel, message, count);
                    }
                });
            }

            return Task.CompletedTask;
        }

        private Task Client_ReactionsCleared(Cacheable<IUserMessage, ulong> _message, ISocketMessageChannel _channel)
        {
            if (_channel is SocketTextChannel)
            {
                var _ = Task.Run(async () =>
                {
                    var channel = _channel as SocketTextChannel;
                    var config = await context.GuildConfigs
                        .Where(e => e.GuildId == channel.Guild.Id)
                        .FirstOrDefaultAsync();

                    // only process if the starboard channel has a value and it's not in the starboard.
                    if (config.StarBoardChannel.HasValue && channel.Id != config.StarBoardChannel)
                    {
                        var message = await _message.GetOrDownloadAsync();

                        await ProcessReaction(config, channel, message, 0);
                    }
                });
            }

            return Task.CompletedTask;
        }
    }
}
