using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using LucoaBot.Models;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace LucoaBot.Listeners
{
    public class StarboardListener
    {
        private readonly ILogger<StarboardListener> _logger;
        private readonly DatabaseContext _context;
        private readonly DiscordSocketClient _client;

        private static readonly Counter StarboardMessageCounter =
            Metrics.CreateCounter("discord_starboard_count", "Number of starboard posts");

        private readonly Emoji _emoji = new Emoji("⭐");
#if !DEBUG
        private const int DefaultThreshold = 3;
#else
        private const int DefaultThreshold = 1;
#endif
        public StarboardListener(ILogger<StarboardListener> logger, DiscordSocketClient client, DatabaseContext context)
        {
            _logger = logger;
            _client = client;
            _context = context;
        }

        public void Initialize()
        {
            _client.MessageDeleted += Client_MessageDeleted;

            _client.ReactionAdded += Client_ReactionAdded;
            _client.ReactionRemoved += Client_ReactionRemoved;
            _client.ReactionsCleared += Client_ReactionsCleared;
        }

        private Task Client_MessageDeleted(Cacheable<IMessage, ulong> message, ISocketMessageChannel socketChannel)
        {
            Task.Run(async () =>
            {
                try
                {
                    if (socketChannel is SocketTextChannel channel)
                    {
                        var config = await _context.GuildConfigs.AsQueryable()
                            .Where(e => e.GuildId == channel.Guild.Id)
                            .FirstOrDefaultAsync();
                        if (config?.StarBoardChannel != null && config.StarBoardChannel != channel.Id &&
                            config.StarBoardChannel != 0)
                        {
                            var starboardChannel = channel.Guild.GetTextChannel(config.StarBoardChannel.Value);
                            var messageId = message.Id.ToString();
                            var messages = starboardChannel.GetMessagesAsync(int.MaxValue).Flatten();

                            var starMessage = await (from m in messages
                                where m.Author.Id == _client.CurrentUser.Id
                                      && m.Embeds.SelectMany(e => e.Fields).Any(f =>
                                          f.Name == "Message ID" && f.Value == messageId)
                                select m).FirstOrDefaultAsync();

                            if (starMessage != null) await starMessage.DeleteAsync();
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception thrown in Client_MessageDeleted");
                }
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }

        private ValueTask<IMessage> FindStarPost(SocketTextChannel starboardChannel, IUserMessage message)
        {
            var messageId = message.Id.ToString();
            var dateThreshold = DateTimeOffset.Now.AddDays(-1);
            var messages = starboardChannel.GetMessagesAsync().Flatten();
            return (from m in messages
                where m.Author.Id == _client.CurrentUser.Id
                      && m.CreatedAt > dateThreshold
                      && m.Embeds.SelectMany(e => e.Fields).Any(f => f.Name == "Message ID" && f.Value == messageId)
                select m).FirstOrDefaultAsync();
        }

        private async Task ProcessReaction(GuildConfig config, SocketTextChannel channel, IUserMessage message,
            int count)
        {
            if (config.StarBoardChannel == null) return;

            var starboardChannel = channel.Guild.GetTextChannel(config.StarBoardChannel.Value);
            if (starboardChannel != null)
            {
                var starboardMessage = await FindStarPost(starboardChannel, message) as IUserMessage;

                if (count < DefaultThreshold)
                {
                    if (starboardMessage != null) await starboardMessage.DeleteAsync();
                }
                else
                {
                    var scale = 255 - Math.Clamp((count - DefaultThreshold) * 25, 0, 255);

                    var embedBuilder = new EmbedBuilder
                    {
                        Title = $"{_emoji} **{count}**",
                        Color = new Color(255, 255, scale),
                        Author = new EmbedAuthorBuilder
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
                                embedBuilder.AddField("SPOILER", attachment.Url);
                            else
                                embedBuilder.ImageUrl = attachment.Url;
                        }
                    }
                    else if (message.Embeds.Any())
                    {
                        var embed = message.Embeds.First();
                        if (embed.Type == EmbedType.Gifv || embed.Type == EmbedType.Image)
                        {
                            if (embed.Image.HasValue)
                                embedBuilder.ImageUrl = embed.Image.Value.Url;
                            else if (embed.Thumbnail.HasValue) embedBuilder.ImageUrl = embed.Thumbnail.Value.Url;
                        }
                    }

                    embedBuilder.AddField("Channel", channel.Mention, true)
                        .AddField("Message ID", message.Id, true)
                        .AddField("Link to message",
                            $"[Click here to go to the original message.]({message.GetJumpUrl()})");

                    if (starboardMessage == null)
                    {
                        StarboardMessageCounter.Inc();
                        await starboardChannel.SendMessageAsync(embed: embedBuilder.Build());
                    }
                    else
                    {
                        await starboardMessage.ModifyAsync(p => p.Embed = embedBuilder.Build());
                    }
                }
            }
        }

        private Task Client_ReactionAdded(Cacheable<IUserMessage, ulong> cacheMessage,
            ISocketMessageChannel socketChannel, SocketReaction reaction)
        {
            if (reaction.Emote.Name != _emoji.Name || !(socketChannel is SocketTextChannel)) return Task.CompletedTask;
            Task.Run(async () =>
            {
                try
                {
                    var channel = socketChannel as SocketTextChannel;
                    var config = await _context.GuildConfigs.AsQueryable()
                        .Where(e => e.GuildId == channel.Guild.Id)
                        .FirstOrDefaultAsync();

                    // only process if the starboard channel has a value and it's not in the starboard.
                    if (channel != null && config.StarBoardChannel.HasValue &&
                        channel.Id != config.StarBoardChannel)
                    {
                        var message = await cacheMessage.GetOrDownloadAsync();

                        // only check the last days of messages
                        var dateThreshold = DateTimeOffset.Now.AddDays(-1);
                        if (message.CreatedAt < dateThreshold)
                            return;

                        if (message.Reactions.TryGetValue(_emoji, out var reactionMetadata))
                            await ProcessReaction(config, channel, message, reactionMetadata.ReactionCount);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception thrown in Client_ReactionAdded");
                }
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }

        private Task Client_ReactionRemoved(Cacheable<IUserMessage, ulong> cacheMessage,
            ISocketMessageChannel socketChannel, SocketReaction reaction)
        {
            if (reaction.Emote.Name != _emoji.Name || !(socketChannel is SocketTextChannel)) return Task.CompletedTask;
            
            Task.Run(async () =>
            {
                try
                {
                    var channel = socketChannel as SocketTextChannel;
                    var config = await _context.GuildConfigs.AsQueryable()
                        .Where(e => e.GuildId == channel.Guild.Id)
                        .FirstOrDefaultAsync();

                    // only process if the starboard channel has a value and it's not in the starboard.
                    if (channel != null && config.StarBoardChannel.HasValue &&
                        channel.Id != config.StarBoardChannel)
                    {
                        var message = await cacheMessage.GetOrDownloadAsync();

                        // only check the last day of messages
                        var dateThreshold = DateTimeOffset.Now.AddDays(-1);
                        if (message.CreatedAt < dateThreshold)
                            return;

                        var count = 0;
                        if (message.Reactions.TryGetValue(_emoji, out var reactionMetadata))
                            count = reactionMetadata.ReactionCount;

                        await ProcessReaction(config, channel, message, count);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception thrown in Client_ReactionRemoved");
                }
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }

        private Task Client_ReactionsCleared(Cacheable<IUserMessage, ulong> cacheMessage,
            ISocketMessageChannel socketChannel)
        {
            if (!(socketChannel is SocketTextChannel)) return Task.CompletedTask;
            Task.Run(async () =>
            {
                try
                {
                    var channel = socketChannel as SocketTextChannel;
                    var config = await _context.GuildConfigs.AsQueryable()
                        .Where(e => e.GuildId == channel.Guild.Id)
                        .FirstOrDefaultAsync();

                    // only process if the starboard channel has a value and it's not in the starboard.
                    if (channel != null && config.StarBoardChannel.HasValue &&
                        channel.Id != config.StarBoardChannel)
                    {
                        var message = await cacheMessage.GetOrDownloadAsync();

                        // only check the last day of messages
                        var dateThreshold = DateTimeOffset.Now.AddDays(-1);
                        if (message.CreatedAt < dateThreshold)
                            return;

                        await ProcessReaction(config, channel, message, 0);
                    }
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Exception thrown in Client_ReactionsCleared");
                }
            }).SafeFireAndForget(false);

            return Task.CompletedTask;
        }
    }
}