using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using SkiaSharp;

namespace LucoaBot.Commands
{
    
    [RequireBotPermissions(Permissions.SendMessages)]
    public class UtilityModule : BaseCommandModule
    {
        private const string BaseUrl = "https://discordapp.com/api/oauth2/authorize";

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
            var builder = new DiscordEmbedBuilder()
            {
                Author = new DiscordEmbedBuilder.EmbedAuthor()
                {
                    Name = $"{context.Guild.Owner.Username}#{context.Guild.Owner.Discriminator}",
                    IconUrl = context.Guild.Owner.AvatarUrl
                },
                Color = new DiscordColor(114, 137, 218),
                Description = context.Guild.Name,
                ThumbnailUrl = context.Guild.IconUrl
            };

            var fields = new Dictionary<string, string>
            {
                {"Id", context.Guild.Id.ToString()},
                {"Region", context.Guild.VoiceRegion.Name},
                {"Categories", context.Guild.Channels.Count(c => c.Value.IsCategory).ToString()},
                {"Text Channels", context.Guild.Channels.Count(c => c.Value.Type == ChannelType.Text).ToString()},
                {"Voice Channels", context.Guild.Channels.Count(c => c.Value.Type == ChannelType.Voice).ToString()},
                {"Total Members", context.Guild.MemberCount.ToString()},
                {
                    "Online Members",
                    context.Guild.Members.Count(e => e.Value.Presence.Status != UserStatus.Offline).ToString()
                },
                {"People", context.Guild.Members.Count(e => !e.Value.IsBot).ToString()},
                {"Bots", context.Guild.Members.Count(e => e.Value.IsBot).ToString()},
                {"Emojis", context.Guild.Emojis.Count.ToString()},
                {"Created At", context.Guild.CreationTimestamp.ToString("r")}
            };

            foreach (var (name, value) in fields)
            {
                builder.AddField(name, value, true);
            }

            await context.RespondAsync(embed: builder.Build());
        }

        [Command("userinfo")]
        [Description("Returns the user's information")]
        [RequireGuild]
        public async Task UserInfoAsync(CommandContext context, DiscordMember user = null)
        {
            if (user == null) user = await context.Guild.GetMemberAsync(context.User.Id);

            var color = user.Presence.Status switch
            {
                UserStatus.Idle => new DiscordColor(250, 166, 26),
                UserStatus.DoNotDisturb => new DiscordColor(240, 71, 71),
                UserStatus.Invisible => new DiscordColor(116, 127, 141),
                UserStatus.Offline => new DiscordColor(116, 127, 141),
                _ => new DiscordColor(67, 181, 129)
            };

            var builder = new DiscordEmbedBuilder()
            {
                Color = color,
                Author = new DiscordEmbedBuilder.EmbedAuthor()
                {
                    Name = $"{user.Username}#{user.Discriminator}",
                    IconUrl = user.AvatarUrl
                },
                ThumbnailUrl = user.AvatarUrl,
                Description = user.Mention,
                Timestamp = DateTimeOffset.Now,
                Footer = new DiscordEmbedBuilder.EmbedFooter()
                {
                    Text = $"ID: {user.Id}"
                }
            };

            // TODO: fix this
            var roles = user.Roles
                .Where(e => e.Name != "Everyone");

            var fields = new Dictionary<string, string>
            {
                {"Status", user.Presence.Status.ToString()},
                {"Joined", user.JoinedAt.ToString("r")},
                {"Registered", user.CreationTimestamp.ToString("r")},
                {$"Roles [{roles.Count()}]", string.Join(" ", roles.Select(e => e.Mention))}
            };

            foreach (var (name, value) in fields)
            {
                builder.AddField(name, value, true);
            }

            if ((user.PermissionsIn(context.Channel) & Permissions.Administrator) != 0)
            {
                builder.AddField("Key Permissions", Permissions.Administrator.ToString(), true);
            }
            else
            {
                var perms = KeyPermissions
                    .Where(e => (user.PermissionsIn(context.Channel) & e) != 0)
                    .Select(e => e.ToString())
                    .ToArray();
                if (perms.Any())
                    builder.AddField("Key Permissions", string.Join(" ", perms), true);
            }

            await context.RespondAsync(embed: builder.Build());
        }

        [Command("stats")]
        [Description("Displays the bot statistics")]
        public async Task StatsAsync(CommandContext context)
        {
            var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            var application = await context.Client.GetCurrentApplicationAsync();

            var embedBuilder = new DiscordEmbedBuilder()
            {
                Color = new DiscordColor(3447003),
                Author = new DiscordEmbedBuilder.EmbedAuthor()
                {
                    Name = $"LucoaBot",
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
        public async Task RollAsync(CommandContext context, int number)
        {
            await context.RespondAsync(
                $"{context.User.Mention} rolled a {RandomNumberGenerator.GetInt32(1, number + 1)}");
        }

        [Command("jumbo")]
        [Description("Takes an emote and makes it larger")]
        public async Task JumboAsync(CommandContext context, DiscordEmoji emote)
        {
            var emoteUri = new Uri(emote.Url);

            var httpClient = _httpClientFactory.CreateClient();
            await using var response = await httpClient.GetStreamAsync(emoteUri);

            if (emote.IsAnimated)
            {
                var filename = Path.GetFileName(emoteUri.LocalPath);
                await context.RespondWithFileAsync(filename, response);
            }
            else
            {
                var filename = Path.GetFileNameWithoutExtension(emoteUri.LocalPath);
                using var bitmap = SKBitmap.Decode(response);
                if (bitmap == null)
                    return;

                var width = Math.Min(128, bitmap.Width * 2);
                var height = Math.Min(128, bitmap.Height * 2);

                var newBitmap = new SKBitmap(new SKImageInfo(width, height, bitmap.ColorType, bitmap.AlphaType));
                if (bitmap.Width < width && bitmap.Height < height)
                {
                    if (!bitmap.ScalePixels(newBitmap, SKFilterQuality.High))
                    {
                        newBitmap = bitmap;
                    }
                }
                else
                {
                    newBitmap = bitmap;
                }

                var data = newBitmap.PeekPixels().Encode(SKWebpEncoderOptions.Default);
                await context.RespondWithFileAsync(filename + ".webp", data.AsStream());
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

            await context.RespondWithFileAsync(Path.GetFileName(avatarUri.LocalPath), response);
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
        
            var embedBuilder = new DiscordEmbedBuilder()
            {
                Title = $"xkcd: {data.safe_title}",
                ImageUrl = data.img,
                Footer = new DiscordEmbedBuilder.EmbedFooter()
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