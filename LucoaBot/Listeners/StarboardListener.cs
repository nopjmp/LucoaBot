using System;
using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.WebSocket;
using LucoaBot.Models;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace LucoaBot.Listeners
{
    public class StarboardListener
    {
        private readonly ILogger<StarboardListener> _logger;
        private readonly IServiceProvider _serviceProvider;

        private readonly DiscordSocketClient _client;

        // TODO: Reaction queue processing
        private readonly BusQueue _busQueue;

        private static readonly Counter StarboardMessageCounter =
            Metrics.CreateCounter("discord_starboard_count", "Number of starboard posts");

        private readonly Emoji _emoji = new Emoji("⭐");
#if !DEBUG
        private const int DefaultThreshold = 3;
#else
        private const int DefaultThreshold = 1;
#endif
        public StarboardListener(ILogger<StarboardListener> logger, DiscordSocketClient client,
            IServiceProvider serviceProvider, BusQueue busQueue)
        {
            _logger = logger;
            _client = client;
            _serviceProvider = serviceProvider;
            _busQueue = busQueue;
        }

        public void Initialize()
        {
            _busQueue.MessageDeleted += Client_MessageDeleted;
            _busQueue.ReactionEvent += OnReactionEvent;
        }

        private async Task<ulong?> GetStarboardChannel(ulong guildId)
        {
            using var scope = _serviceProvider.CreateScope();
            await using var context = scope.ServiceProvider.GetService<DatabaseContext>();
            var config = await context.GuildConfigs.AsNoTracking()
                .Where(e => e.GuildId == guildId)
                .FirstOrDefaultAsync();

            return config.StarBoardChannel;
        }

        private async Task Client_MessageDeleted(RawMessage rawMessage)
        {
            var socketChannel = rawMessage.GetChannel(_client);
            try
            {
                if (socketChannel is SocketTextChannel channel)
                {
                    var starboardChannelId = await GetStarboardChannel(channel.Guild.Id);
                    if (starboardChannelId != null && starboardChannelId != channel.Id &&
                        starboardChannelId != 0)
                    {
                        var starboardChannel = channel.Guild.GetTextChannel(starboardChannelId.Value);
                        var messageId = rawMessage.MessageId.ToString();
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
        }

        private ValueTask<IMessage> FindStarPost(SocketTextChannel starboardChannel, IMessage message)
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

        private async Task ProcessReaction(ulong? starboardChannelId, SocketTextChannel channel, IMessage message,
            int count)
        {
            if (starboardChannelId == null) return;

            var starboardChannel = channel.Guild.GetTextChannel(starboardChannelId.Value);
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

        private async Task OnReactionEvent(ReactionMessage reactionMessage)
        {
            if (string.IsNullOrEmpty(reactionMessage.Emote) || reactionMessage.Emote == _emoji.Name)
            {
                var starboardChannelId = await GetStarboardChannel(reactionMessage.GuildId);
                // only process if the starboard channel has a value and it's not in the starboard.
                if (starboardChannelId.HasValue && reactionMessage.ChannelId != starboardChannelId)
                {
                    var socketChannel = _client.GetChannel(reactionMessage.ChannelId);
                    if (socketChannel is SocketTextChannel channel)
                    {
                        var message = await channel.GetMessageAsync(reactionMessage.MessageId);
                        // only check the last days of messages
                        var dateThreshold = DateTimeOffset.Now.AddDays(-1);
                        if (message.CreatedAt < dateThreshold)
                            return;

                        var reactionCount = 0;
                        if (reactionMessage.ReactionAction != ReactionAction.Cleared)
                        {
                            message.Reactions.TryGetValue(_emoji, out var reactionMetadata);
                            reactionCount = reactionMetadata.ReactionCount;
                        }

                        await ProcessReaction(starboardChannelId, channel, message, reactionCount);
                    }
                }
            }
        }
    }
}