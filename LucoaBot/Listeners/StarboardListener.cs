using System;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using DSharpPlus.EventArgs;
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

        private readonly DiscordClient _client;

        // TODO: Reaction queue processing

        private readonly DiscordEmoji _emoji = DiscordEmoji.FromUnicode("⭐");
#if !DEBUG
        private const int DefaultThreshold = 3;
#else
        private const int DefaultThreshold = 1;
#endif
        public StarboardListener(ILogger<StarboardListener> logger, DiscordClient client,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _client = client;
            _serviceProvider = serviceProvider;
        }

        public void Initialize()
        {
            _client.MessageDeleted += Client_MessageDeleted;
            _client.MessageReactionAdded += ClientOnMessageReactionAdded;
            _client.MessageReactionRemoved += ClientOnMessageReactionRemoved;
            _client.MessageReactionsCleared += ClientOnMessageReactionsCleared;
            _client.MessageReactionRemovedEmoji += ClientOnMessageReactionRemovedEmoji;
        }

        private Task ClientOnMessageReactionRemovedEmoji(MessageReactionRemoveEmojiEventArgs args)
        {
            if (args.Message.Author.IsBot) return Task.CompletedTask;

            return OnReactionEvent(args.Emoji, true, args.Guild, args.Message);
        }

        private Task ClientOnMessageReactionAdded(MessageReactionAddEventArgs args)
        {
            // ignore bot messages
            if (args.Message.Author.IsBot) return Task.CompletedTask;
            
            return OnReactionEvent(args.Emoji, false, args.Guild, args.Message);
        }

        private Task ClientOnMessageReactionRemoved(MessageReactionRemoveEventArgs args)
        {
            // ignore bot messages
            if (args.Message.Author.IsBot) return Task.CompletedTask;
            
            return OnReactionEvent(args.Emoji, false, args.Guild, args.Message);
        }

        private Task ClientOnMessageReactionsCleared(MessageReactionsClearEventArgs args)
        {
            // ignore bot messages
            if (args.Message.Author.IsBot) return Task.CompletedTask;
            
            return OnReactionEvent(_emoji, true, args.Guild, args.Message);
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

        private async Task Client_MessageDeleted(MessageDeleteEventArgs args)
        {
            // ignore bot messages
            if (args.Message.Author.IsBot) return;
            
            try
            {
                var starboardChannelId = await GetStarboardChannel(args.Guild.Id);
                if (starboardChannelId != null && starboardChannelId != args.Channel.Id &&
                    starboardChannelId != 0)
                {
                    var starboardChannel = args.Guild.GetChannel(starboardChannelId.Value);
                    var messageId = args.Message.Id.ToString();

                    // TODO: optimize this with a cache when we hit 100+ servers with star msg id -> chan,msg id
                    var messages = await starboardChannel.GetMessagesAsync();
                    while (messages.Count > 0)
                    {
                        var starMessage = (from m in messages
                            where m.Author.Id == _client.CurrentUser.Id
                                  && m.Embeds
                                      .Where(e => e.Fields != null)
                                      .SelectMany(e => e.Fields)
                                      .Any(f => f.Name == "Message ID" && f.Value == messageId)
                            select m).FirstOrDefault();

                        if (starMessage != null)
                        {
                            await starMessage.DeleteAsync();
                            break;
                        }

                        messages = await starboardChannel.GetMessagesBeforeAsync(messages.Last().Id);
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception thrown in Client_MessageDeleted");
            }
        }

        private async ValueTask<DiscordMessage> FindStarPost(DiscordChannel starboardChannel, DiscordMessage message)
        {
            var messageId = message.Id.ToString();
            var dateThreshold = DateTimeOffset.Now.AddDays(-1);
            var messages = await starboardChannel.GetMessagesAsync();
            return (from m in messages
                where m.Author.Id == _client.CurrentUser.Id
                      && m.CreationTimestamp > dateThreshold
                      && m.Embeds.SelectMany(e => e.Fields).Any(f => f.Name == "Message ID" && f.Value == messageId)
                select m).FirstOrDefault();
        }

        private async Task ProcessReaction(ulong starboardChannelId, DiscordChannel channel, DiscordMessage message,
            int count)
        {
            var starboardChannel = channel.Guild.GetChannel(starboardChannelId);
            if (starboardChannel != null)
            {
                var starboardMessage = await FindStarPost(starboardChannel, message);

                if (count < DefaultThreshold)
                {
                    if (starboardMessage != null) await starboardMessage.DeleteAsync();
                }
                else
                {
                    var scale = (byte) (255 - Math.Clamp((count - DefaultThreshold) * 25, 0, 255));

                    var embedBuilder = new DiscordEmbedBuilder()
                    {
                        Title = $"{_emoji} **{count}**",
                        Color = new DiscordColor(255, 255, scale),
                        Author = new DiscordEmbedBuilder.EmbedAuthor()
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
                        await starboardChannel.SendMessageAsync(embed: embedBuilder.Build());
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
            if (emoji.Equals(_emoji))
            {
                var starboardChannelId = await GetStarboardChannel(guild.Id);
                // only process if the starboard channel has a value and it's not in the starboard.
                if (starboardChannelId.HasValue && message.ChannelId != starboardChannelId.Value)
                {
                    // only check the last days of messages
                    var dateThreshold = DateTimeOffset.Now.AddDays(-1);
                    if (message.CreationTimestamp < dateThreshold)
                        return;

                    var reactionCount = 0;
                    if (!cleared)
                    {
                        reactionCount = message.Reactions.Count(e => e.Emoji.Equals(_emoji));
                    }

                    // Fetch the message contents
                    message = await message.Channel.GetMessageAsync(message.Id);
                    await ProcessReaction(starboardChannelId.Value, message.Channel, message, reactionCount);
                }
            }
        }
    }
}