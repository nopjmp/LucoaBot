using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
using LucoaBot.Data;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LucoaBot.Listeners
{
    public class StarboardListener
    {
        private readonly ILogger<StarboardListener> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly DatabaseContext _database;
        private readonly DiscordClient _client;

        // TODO: Reaction queue processing

        private readonly DiscordEmoji _emoji = DiscordEmoji.FromUnicode("⭐");
#if !DEBUG
        private const int DefaultThreshold = 3;
#else
        private const int DefaultThreshold = 1;
#endif
        public StarboardListener(ILogger<StarboardListener> logger, DiscordClient client,
            IServiceProvider serviceProvider, DatabaseContext database)
        {
            _logger = logger;
            _client = client;
            _serviceProvider = serviceProvider;
            _database = database;
        }

        public void Initialize()
        {
            _client.MessageDeleted += (_, e) =>
            {
                Client_MessageDeleted(e).Forget();
                return Task.CompletedTask;
            };
            _client.MessageReactionAdded += (_, e) =>
            {
                ClientOnMessageReactionAdded(e).Forget();
                return Task.CompletedTask;
            };
            _client.MessageReactionRemoved += (_, e) =>
            {
                ClientOnMessageReactionRemoved(e).Forget();
                return Task.CompletedTask;
            };
            _client.MessageReactionsCleared += (_, e) =>
            {
                ClientOnMessageReactionsCleared(e).Forget();
                return Task.CompletedTask;
            };
            _client.MessageReactionRemovedEmoji += (_, e) =>
            {
                ClientOnMessageReactionRemovedEmoji(e).Forget();
                return Task.CompletedTask;
            };
        }

        private Task ClientOnMessageReactionRemovedEmoji(MessageReactionRemoveEmojiEventArgs args)
        {
            return OnReactionEvent(args.Emoji, true, args.Guild, args.Message);
        }

        private Task ClientOnMessageReactionAdded(MessageReactionAddEventArgs args)
        {
            return OnReactionEvent(args.Emoji, false, args.Guild, args.Message);
        }

        private Task ClientOnMessageReactionRemoved(MessageReactionRemoveEventArgs args)
        {
            return OnReactionEvent(args.Emoji, false, args.Guild, args.Message);
        }

        private Task ClientOnMessageReactionsCleared(MessageReactionsClearEventArgs args)
        {
            return OnReactionEvent(_emoji, true, args.Guild, args.Message);
        }


        private async Task<ulong?> GetStarboardChannel(ulong guildId)
        {
            using var scope = _serviceProvider.CreateScope();
            await using var context = scope.ServiceProvider.GetService<DatabaseContext>();
            var config = await context.GuildConfigs.AsNoTracking()
                .Where(e => e.GuildId == guildId)
                .FirstOrDefaultAsync();

            return config?.StarBoardChannel;
        }

        private async Task Client_MessageDeleted(MessageDeleteEventArgs args)
        {
            try
            {
                var starboardChannelId = await GetStarboardChannel(args.Guild.Id);
                if (starboardChannelId != null && starboardChannelId != args.Channel.Id &&
                    starboardChannelId != 0)
                {
                    var starboardChannel = args.Guild.GetChannel(starboardChannelId.Value);

                    var starMessage = await FindStarPost(starboardChannel, args.Message.Id, false);

                    // TODO: use the cacheEntry from the FindStarPost
                    var cacheEntry = await _database.StarboardCache.FirstOrDefaultAsync(e => e.MessageId == args.Message.Id && e.GuildId == starboardChannel.GuildId);
                    if (cacheEntry != null)
                    {
                        _database.StarboardCache.Remove(cacheEntry);
                        await _database.SaveChangesAsync();
                    }
                    if (starMessage != null) await starMessage.DeleteAsync();
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown in Client_MessageDeleted");
            }
        }

        private static readonly IReadOnlyList<DiscordEmbedField> EmptyEmbedFields = new List<DiscordEmbedField>();

        private async Task<DiscordMessage> FindStarPost(DiscordChannel starboardChannel, ulong messageId,
            bool timeLimited = true)
        {
            var dateThreshold = DateTimeOffset.Now.AddDays(-1);

            // TODO: optimize this with a cache when we hit 100+ servers with star msg id -> chan,msg id


            var cacheEntry = await _database.StarboardCache.FirstOrDefaultAsync(e => e.MessageId == messageId && e.GuildId == starboardChannel.GuildId);

            if (cacheEntry != null)
            {
                var message = await starboardChannel.GetMessageAsync(cacheEntry.StarboardId);
                return message;
            }

            var messageIdStr = messageId.ToString();
            var messages = await starboardChannel.GetMessagesAsync();
            var count = 0;
            while (messages.Count > 0)
            {
                foreach (var message in messages)
                {
                    count++;

                    // break when message is too old.
                    if (timeLimited && message.CreationTimestamp <= dateThreshold) return null;

                    if (message.Author.Id == _client.CurrentUser.Id && message.Embeds
                        .SelectMany(e => e.Fields ?? EmptyEmbedFields)
                        .Any(f => f.Name == "Message ID" && f.Value == messageIdStr))
                        return message;
                }

                // break when we hit 400 messages
                if (count > 400) return null;

                messages = await starboardChannel.GetMessagesBeforeAsync(messages.Last().Id);
            }

            return null;
        }

        private async Task ProcessReaction(ulong starboardChannelId, DiscordChannel channel, DiscordMessage message,
            int count)
        {
            var starboardChannel = channel.Guild.GetChannel(starboardChannelId);
            if (starboardChannel != null && starboardChannel.GuildId.HasValue)
            {
                var starboardMessage = await FindStarPost(starboardChannel, message.Id);

                if (count < DefaultThreshold)
                {
                    if (starboardMessage != null)
                    {
                        var cacheEntry = await _database.StarboardCache.FirstOrDefaultAsync(e => e.MessageId == message.Id && e.GuildId == starboardChannel.GuildId);
                        if (cacheEntry != null)
                        {
                            _database.StarboardCache.Remove(cacheEntry);
                            await _database.SaveChangesAsync();
                        }
                        await starboardMessage.DeleteAsync();
                    }
                }
                else
                {
                    var scale = (byte)(255 - Math.Clamp((count - DefaultThreshold) * 25, 0, 255));

                    var embedBuilder = new DiscordEmbedBuilder
                    {
                        Title = $"{_emoji} **{count}**",
                        Color = new DiscordColor(255, 255, scale),
                        Author = new DiscordEmbedBuilder.EmbedAuthor
                        {
                            Name = $"{message.Author.Username}#{message.Author.Discriminator}",
                            IconUrl = message.Author.AvatarUrl
                        },
                        Description = message.Content,
                        Timestamp = message.CreationTimestamp
                    };

                    if (message.Attachments.Any())
                    {
                        var attachment = message.Attachments.First();
                        if (attachment.Url != null)
                        {
                            if (attachment.FileName.StartsWith("SPOILER_"))
                                embedBuilder.AddField("SPOILER", attachment.Url);
                            else
                                embedBuilder.ImageUrl = attachment.Url;
                        }
                    }
                    else if (message.Embeds.Any())
                    {
                        var embed = message.Embeds.First();
                        if (embed.Type == "gifv" || embed.Type == "image")
                        {
                            if (embed.Image != null)
                                embedBuilder.ImageUrl = embed.Image.Url.ToString();
                            else if (embed.Thumbnail != null) embedBuilder.ImageUrl = embed.Thumbnail.Url.ToString();
                        }
                    }

                    embedBuilder.AddField("Channel", channel.Mention, true)
                        .AddField("Message ID", message.Id.ToString(), true)
                        .AddField("Link to message",
                            $"[Click here to go to the original message.]({message.JumpLink})");

                    if (starboardMessage == null)
                    {
                        starboardMessage = await starboardChannel.SendMessageAsync(embed: embedBuilder.Build());
                        var cacheEntry = new StarboardCache
                        {
                            StarboardId = starboardMessage.Id,
                            GuildId = starboardChannel.GuildId.Value,
                            MessageId = message.Id
                        };
                        await _database.StarboardCache.AddAsync(cacheEntry);
                        await _database.SaveChangesAsync();
                    }
                    else
                    {
                        await starboardMessage.ModifyAsync(embed: embedBuilder.Build());
                    }
                }
            }
        }

        private async Task OnReactionEvent(DiscordEmoji emoji, bool cleared, DiscordGuild guild, DiscordMessage message)
        {
            // ignore DM reactions
            if (guild == null) return;

            if (emoji.Equals(_emoji))
            {
                var starboardChannelId = await GetStarboardChannel(guild.Id);
                // only process if the starboard channel has a value and it's not in the starboard.
                if (starboardChannelId.HasValue)
                {
                    // Fetch the message contents
                    message = await message.Channel.GetMessageAsync(message.Id);

                    // ignore bot messages
                    if (message.Author.IsBot || message.ChannelId == starboardChannelId.Value) return;

                    // only check the last days of messages
                    var dateThreshold = DateTimeOffset.Now.AddDays(-1);
                    if (message.CreationTimestamp < dateThreshold)
                        return;

                    var reactionCount = 0;
                    if (!cleared)
                    {
                        DiscordReaction reaction = null;
                        foreach (var e in message.Reactions)
                            if (e.Emoji.Equals(_emoji))
                            {
                                reaction = e;
                                break;
                            }

                        reactionCount = reaction?.Count ?? 0;
                    }

                    await ProcessReaction(starboardChannelId.Value, message.Channel, message, reactionCount);
                }
            }
        }
    }
}