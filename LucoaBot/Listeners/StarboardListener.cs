using Discord;
using Discord.WebSocket;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prometheus;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LucoaBot.Listeners
{
    public class StarboardListener
    {
        private readonly ILogger<StarboardListener> logger;
        private readonly DatabaseContext context;
        private readonly DiscordSocketClient client;

        private static readonly Counter starboardMessageCounter = Metrics.CreateCounter("discord_starboard_count", "Number of starboard posts");

        private readonly Emoji emoji = new Emoji("⭐");
#if !DEBUG
        private readonly static int DEFAULT_THRESHOLD = 3;
#else
        private readonly static int DEFAULT_THRESHOLD = 1;
#endif
        public StarboardListener(ILogger<StarboardListener> logger, DiscordSocketClient client, DatabaseContext context)
        {
            this.logger = logger;
            this.client = client;
            this.context = context;
        }

        public void Initialize()
        {
            client.MessageDeleted += Client_MessageDeleted;

            client.ReactionAdded += Client_ReactionAdded;
            client.ReactionRemoved += Client_ReactionRemoved;
            client.ReactionsCleared += Client_ReactionsCleared;
        }

        private async Task Client_MessageDeleted(Cacheable<IMessage, ulong> _message, ISocketMessageChannel _channel)
        {
            try
            {
                if (_channel is SocketTextChannel)
                {
                    var channel = _channel as SocketTextChannel;
                    var config = await context.GuildConfigs
                                    .Where(e => e.GuildId == channel.Guild.Id)
                                    .FirstOrDefaultAsync();
                    if (config != null && config.StarBoardChannel != null
                        && config.StarBoardChannel != _channel.Id && config.StarBoardChannel != 0)
                    {
                        var starboardChannel = channel.Guild.GetTextChannel(config.StarBoardChannel.Value);
                        var messageId = _message.Id.ToString();
                        var messages = starboardChannel.GetMessagesAsync(limit: int.MaxValue).Flatten();

                        var starMessage = await (from m in messages
                                                 where m.Author.Id == client.CurrentUser.Id
                                                      && m.Embeds.SelectMany(e => e.Fields).Any(f => f.Name == "Message ID" && f.Value == messageId)
                                                 select m).FirstOrDefault();

                        if (starMessage != null)
                        {
                            await starMessage.DeleteAsync();
                        }
                    }
                }
            }
            catch(Exception e)
            {
                logger.LogError(e, "Exception thrown in Client_MessageDeleted");
            }
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
                            case EmbedType.Gifv:
                            case EmbedType.Image:
                                if (embed.Image.HasValue)
                                {
                                    embedBuilder.ImageUrl = embed.Image.Value.Url;
                                }
                                else if (embed.Thumbnail.HasValue)
                                {
                                    embedBuilder.ImageUrl = embed.Thumbnail.Value.Url;
                                }
                                break;
                        }
                    }
                    embedBuilder.AddField("Channel", channel.Mention, true)
                                .AddField("Message ID", message.Id, true)
                                .AddField("Link to message", $"[Click here to go to the original message.]({message.GetJumpUrl()})", false);

                    if (starboardMessage == null)
                    {
                        starboardMessageCounter.Inc();
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
                Task.Run(async () =>
                {
                    try
                    {
                        var channel = _channel as SocketTextChannel;
                        var config = await context.GuildConfigs
                            .Where(e => e.GuildId == channel.Guild.Id)
                            .FirstOrDefaultAsync();

                        // only process if the starboard channel has a value and it's not in the starboard.
                        if (config.StarBoardChannel.HasValue && channel.Id != config.StarBoardChannel)
                        {
                            var message = await _message.GetOrDownloadAsync();

                            // only check the last days of messages
                            var dateThreshold = DateTimeOffset.Now.AddDays(-1);
                            if (message.CreatedAt < dateThreshold)
                                return;

                            ReactionMetadata reactionMetadata;
                            if (message.Reactions.TryGetValue(emoji, out reactionMetadata))
                            {
                                await ProcessReaction(config, channel, message, reactionMetadata.ReactionCount);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Exception thrown in Client_ReactionAdded");
                    }
                }).SafeFireAndForget(false);
            }

            return Task.CompletedTask;
        }

        private Task Client_ReactionRemoved(Cacheable<IUserMessage, ulong> _message, ISocketMessageChannel _channel, SocketReaction reaction)
        {
            if (reaction.Emote.Name == emoji.Name
                && _channel is SocketTextChannel)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var channel = _channel as SocketTextChannel;
                        var config = await context.GuildConfigs
                            .Where(e => e.GuildId == channel.Guild.Id)
                            .FirstOrDefaultAsync();

                        // only process if the starboard channel has a value and it's not in the starboard.
                        if (config.StarBoardChannel.HasValue && channel.Id != config.StarBoardChannel)
                        {
                            var message = await _message.GetOrDownloadAsync();

                            // only check the last day of messages
                            var dateThreshold = DateTimeOffset.Now.AddDays(-1);
                            if (message.CreatedAt < dateThreshold)
                                return;

                            var count = 0;
                            ReactionMetadata reactionMetadata;
                            if (message.Reactions.TryGetValue(emoji, out reactionMetadata))
                            {
                                count = reactionMetadata.ReactionCount;
                            }

                            await ProcessReaction(config, channel, message, count);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Exception thrown in Client_ReactionRemoved");
                    }
                }).SafeFireAndForget(false);
            }

            return Task.CompletedTask;
        }

        private Task Client_ReactionsCleared(Cacheable<IUserMessage, ulong> _message, ISocketMessageChannel _channel)
        {
            if (_channel is SocketTextChannel)
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var channel = _channel as SocketTextChannel;
                        var config = await context.GuildConfigs
                            .Where(e => e.GuildId == channel.Guild.Id)
                            .FirstOrDefaultAsync();

                        // only process if the starboard channel has a value and it's not in the starboard.
                        if (config.StarBoardChannel.HasValue && channel.Id != config.StarBoardChannel)
                        {
                            var message = await _message.GetOrDownloadAsync();

                            // only check the last day of messages
                            var dateThreshold = DateTimeOffset.Now.AddDays(-1);
                            if (message.CreatedAt < dateThreshold)
                                return;

                            await ProcessReaction(config, channel, message, 0);
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Exception thrown in Client_ReactionsCleared");
                    }
                }).SafeFireAndForget(false);
            }

            return Task.CompletedTask;
        }
    }
}
