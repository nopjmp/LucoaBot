using Dice;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using LucoaBot.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

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
            Permissions.ManageNicknames, Permissions.MentionEveryone, Permissions.ManageEmojis,
            Permissions.ManageThreads
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

            var regions = await context.Guild.ListVoiceRegionsAsync();
            // var voiceChannels = context.Guild.Channels.Values.Where(c => c.Type == ChannelType.Voice);
            // var voiceChannelRegions = voiceChannels.Select(c => c.RtcRegion?.Name);
            var fields = new Dictionary<string, string>
            {
                {"Id", context.Guild.Id.ToString()},
                // {"Voice Region(s)", string.Join(", ", voiceChannelRegions)},
                {"Categories", context.Guild.Channels.Count(c => c.Value.IsCategory).ToString()},
                {"Text Channels", context.Guild.Channels.Count(c => c.Value.Type == ChannelType.Text).ToString()},
                {"Voice Channels", context.Guild.Channels.Count(c => c.Value.Type == ChannelType.Voice).ToString()},
                {"Total Members", context.Guild.MemberCount.ToString()},
                {"Large", context.Guild.IsLarge.ToString()},
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
                // Anti-cheat
                if (int.TryParse(rollExpression, out var num))
                {
                    rollExpression = $"d{num}";
                }
 
                var result = Roller.Roll(rollExpression);
                await context.RespondAsync(
                    $"{context.User.Mention} rolled {result.Value}");
            }
            catch(DiceException e)
            {
                await context.RespondAsync($"{context.User.Mention} {e.Message}");
            }
        }

        [Command("jumbo")]
        [Description("Takes an emote and makes it larger")]
        public async Task JumboAsync(CommandContext context, DiscordEmoji emote)
        {
            try
            {
                if (emote.Id == 0)
                    return;

                var emoteUri = new Uri(emote.GetEmojiURL());

                using var httpClient = _httpClientFactory.CreateClient();
                await using var response = await httpClient.GetStreamAsync(emoteUri);

                // Workaround httpClient response streams not being allowed to seek around.
                await using var imageStream = new MemoryStream();
                await response.CopyToAsync(imageStream);
                imageStream.Seek(0, SeekOrigin.Begin);

                var builder = new DiscordMessageBuilder();
                if (emote.IsAnimated)
                {
                    var filename = Path.GetFileName(emoteUri.LocalPath);
                    await context.RespondAsync(builder.WithFile(filename, imageStream));
                    return;
                }
                else
                {
                    var filename = Path.GetFileNameWithoutExtension(emoteUri.LocalPath);
                    using (var image = Image.Load(imageStream))
                    {
                        var scale = 128.0f / image.Height;
                        image.Mutate(x => x.Resize((int)(image.Width * scale), (int)(image.Height * scale)));
                        
                        await using MemoryStream outputStream = new MemoryStream();

                        await image.SaveAsWebpAsync(outputStream);
                        outputStream.Seek(0, SeekOrigin.Begin);

                        await context.RespondAsync(builder.WithFile(filename + ".webp", outputStream));
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

            var builder = new DiscordEmbedBuilder
            {
                Color = color,
                Author = new DiscordEmbedBuilder.EmbedAuthor
                {
                    Name = $"{user.Username}#{user.Discriminator}",
                    IconUrl = user.AvatarUrl
                },
                Thumbnail = new DiscordEmbedBuilder.EmbedThumbnail
                {
                    Width = 0,
                    Height = 0,
                    Url = user.AvatarUrl
                },
                Description = user.Mention,
                Timestamp = DateTimeOffset.Now,
                Footer = new DiscordEmbedBuilder.EmbedFooter
                {
                    Text = $"ID: {user.Id}"
                }
            };

            // TODO: fix this
            var roles = user.Roles
                .Where(e => e.Name != "Everyone")
                .ToImmutableList();

            var rolesStr = string.Join(" ", roles.Select(e => e.Mention));
            if (string.IsNullOrEmpty(rolesStr))
                rolesStr = "(empty)";

            var fields = new Dictionary<string, string>
            {
                {"Status", user.Presence.Status.ToString()},
                {"Joined", user.JoinedAt.ToString("r")},
                {"Registered", user.CreationTimestamp.ToString("r")},
                {$"Roles [{roles.Count}]", rolesStr}
            };

            foreach (var (name, value) in fields) builder.AddField(name, value, true);

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