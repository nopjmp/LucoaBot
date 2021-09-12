using Dice;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using LucoaBot.Extensions;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;

namespace LucoaBot.Commands
{
    [RequireBotPermissions(Permissions.SendMessages)]
    public class UtilityModule : BaseCommandModule
    {
        private const string BaseUrl = "https://discord.com/api/oauth2/authorize";

        private static readonly List<Permissions> KeyPermissions = new List<Permissions>
        {
            Permissions.KickMembers, Permissions.BanMembers, Permissions.ManageChannels,
            Permissions.ManageRoles, Permissions.ManageGuild, Permissions.ManageWebhooks,
            Permissions.ManageNicknames, Permissions.MentionEveryone, Permissions.ManageEmojis
        };

        private readonly IHttpClientFactory _httpClientFactory;

        public UtilityModule(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [Command("id")]
        [Description("Displays your Discord snowflake.")]
        public Task IdAsync(CommandContext context)
        {
            return context.RespondAsync($"ID: {context.User.Id}");
        }

        [Command("ping")]
        [Description("Returns the latency of the bot to Discord.")]
        public async Task PingAsync(CommandContext context)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var m = await context.RespondAsync("Ping: ...");
            stopwatch.Stop();
            await m.ModifyAsync($"Pong: {stopwatch.ElapsedMilliseconds}ms | Gateway {context.Client.Ping}ms");
        }

        [Command("serverinfo")]
        [Description("Returns the server's information")]
        [RequireGuild]
        public async Task ServerInfoAsync(CommandContext context)
        {
            var builder = new DiscordEmbedBuilder
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"{context.Guild.Owner.Username}#{context.Guild.Owner.Discriminator}",
                    IconUrl = context.Guild.Owner.AvatarUrl
                },
                Color = new DiscordColor(114, 137, 218),
                Description = context.Guild.Name,
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Width = 0,
                    Height = 0,
                    Url = context.Guild.IconUrl
                }
            };

            var fields = new Dictionary<string, string>
            {
                {"Id", context.Guild.Id.ToString()},
                {"Region", context.Guild.VoiceRegion.Name},
                {"Categories", context.Guild.Channels.Count(c => c.Value.IsCategory).ToString()},
                {"Text Channels", context.Guild.Channels.Count(c => c.Value.Type == ChannelType.Text).ToString()},
                {"Voice Channels", context.Guild.Channels.Count(c => c.Value.Type == ChannelType.Voice).ToString()},
                {"Total Members", context.Guild.MemberCount.ToString()},
                // {"People", context.Guild.Members.Count(e => !e.Value.IsBot).ToString()},
                // {"Bots", context.Guild.Members.Count(e => e.Value.IsBot).ToString()},
                {"Emojis", context.Guild.Emojis.Count.ToString()},
                {"Created At", context.Guild.CreationTimestamp.ToString("r")}
            };

            foreach (var (name, value) in fields) builder.AddField(name, value, true);

            await context.RespondAsync(embed: builder.Build());
        }

        [Command("stats")]
        [Description("Displays the bot statistics")]
        public async Task StatsAsync(CommandContext context)
        {
            var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            var application = await context.Client.GetCurrentApplicationAsync();

            var embedBuilder = new DiscordEmbedBuilder
            {
                Color = new DiscordColor(3447003),
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = "LucoaBot",
                    IconUrl = context.Client.CurrentUser.AvatarUrl
                }
            };

            var owner = application.Owners.First();

            embedBuilder.AddField("Owner", $"{owner.Username}#{owner.Discriminator}")
                .AddField("Uptime", uptime.ToHumanTimeString(2))
                .AddField("Bot ID", context.Client.CurrentUser.Id.ToString())
                .AddField("Ping", $"{context.Client.Ping}ms")
                .AddField("Guilds", context.Client.Guilds.Count.ToString())
                .AddField("Total Members",
                    context.Client.Guilds.Aggregate(0, (a, g) => a + g.Value.MemberCount).ToString());

            await context.RespondAsync(embed: embedBuilder.Build());
        }

        [Command("invite")]
        [Description("Generates an invite link for adding the bot to your Discord")]
        public async Task InviteAsync(CommandContext context)
        {
            var applicationInfo = await context.Client.GetCurrentApplicationAsync();
            await context.RespondAsync($"{BaseUrl}?client_id={applicationInfo.Id}&permissions=8&scope=bot");
        }

        [Command("roll")]
        [Description("Generates a number between 1 and the number specified")]
        public async Task RollAsync(CommandContext context, string rollExpression)
        {
            try
            {
                var result = Roller.Roll(rollExpression);
                await context.RespondAsync(
                    $"{context.User.Mention} rolled {result.Value}");
            }
            catch(DiceException e)
            {
                await context.RespondAsync($"{context.User.Mention} invalid roll expression {e}");
            }
        }

        [Command("jumbo")]
        [Description("Takes an emote and makes it larger")]
        public async Task JumboAsync(CommandContext context, DiscordEmoji emote)
        {
            try
            {
                var emoteUri = new Uri(emote.GetEmojiURL());

                using var httpClient = _httpClientFactory.CreateClient();
                await using var response = await httpClient.GetStreamAsync(emoteUri);

                // Workaround httpClient response streams not being allowed to seek around.
                await using var stream = new MemoryStream();
                await response.CopyToAsync(stream);
                stream.Seek(0, SeekOrigin.Begin);

                var builder = new DiscordMessageBuilder();
                if (emote.IsAnimated)
                {
                    var filename = Path.GetFileName(emoteUri.LocalPath);
                    await context.RespondAsync(builder.WithFile(filename, stream));
                    return;
                }
                else if (emote.Id == 0)
                {
                    var filename = Path.GetFileNameWithoutExtension(emoteUri.LocalPath);
                    using var svg = new SKSvg();
                    if (svg.Load(stream) is { })
                    {
                        float scaleX = 128 / svg.Picture.CullRect.Height;
                        float scaleY = 128 / svg.Picture.CullRect.Width;
                        using var bitmap = svg.Picture.ToBitmap(SKColors.Transparent, scaleX, scaleY, SKColorType.Rgba8888, SKAlphaType.Premul, SKColorSpace.CreateSrgb());
                        using var image = SKImage.FromBitmap(bitmap);
                        using var _stream = image.Encode(SKEncodedImageFormat.Webp, 90).AsStream(true);
                        await context.RespondAsync(builder.WithFile(filename + ".webp", _stream));
                    }
                }
                else
                {
                    var filename = Path.GetFileNameWithoutExtension(emoteUri.LocalPath);
                    using var bitmap = SKBitmap.Decode(stream);
                    if (bitmap == null)
                        return;

                    var scale = 128.0f / bitmap.Height;
                    using var destination = new SKBitmap((int)(bitmap.Width * scale), (int)(bitmap.Height * scale));
                    if (bitmap.ScalePixels(destination, SKFilterQuality.High))
                    {
                        using var image = SKImage.FromBitmap(destination);
                        using var _stream = image.Encode(SKEncodedImageFormat.Webp, 90).AsStream(true);
                        await context.RespondAsync(builder.WithFile(filename + ".webp", _stream));
                    }
                }
            }
            catch(InvalidOperationException)
            {
                // Do nothing
            }
        }

        [Command("avatar")]
        [Description("If specified, displays the user's avatar; else, displays your avatar")]
        public async Task AvatarAsync(CommandContext context, DiscordUser user = null)
        {
            if (user == null) user = context.User;

            var avatarUri = new Uri(user.AvatarUrl);

            var httpClient = _httpClientFactory.CreateClient();
            await using var response = await httpClient.GetStreamAsync(avatarUri);

            await context.RespondAsync(new DiscordMessageBuilder().WithFile(Path.GetFileName(avatarUri.LocalPath), response));
        }

        [Command("xkcd")]
        [Description("Fetches an xkcd comic. Random for a random id")]
        public async Task XKCDAsync(CommandContext context, string id = null)
        {
            var httpClient = _httpClientFactory.CreateClient();

            await using var response = await httpClient.GetStreamAsync("https://xkcd.com/info.0.json");
            var data = await JsonSerializer.DeserializeAsync<XKCDData>(response);

            if (id != null)
            {
                var num = id.StartsWith("rand") ? RandomNumberGenerator.GetInt32(1, data.num + 1) : int.Parse(id);

                await using var numResponse = await httpClient.GetStreamAsync($"https://xkcd.com/{num}/info.0.json");
                data = await JsonSerializer.DeserializeAsync<XKCDData>(numResponse);
            }

            var embedBuilder = new DiscordEmbedBuilder
            {
                Title = $"xkcd: {data.safe_title}",
                ImageUrl = data.img,
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = data.alt
                }
            };

            await context.RespondAsync(embed: embedBuilder.Build());
        }

        private struct XKCDData
        {
            public int num { get; set; }
            public string safe_title { get; set; }
            public string alt { get; set; }
            public string img { get; set; }
        }
    }
}