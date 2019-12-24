using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;

namespace LucoaBot.Commands
{
    [Name("Utility")]
    [RequireBotPermission(ChannelPermission.SendMessages)]
    public class UtilityModule : ModuleBase<SocketCommandContext>
    {
        private const string BaseUrl = "https://discordapp.com/api/oauth2/authorize";

        private static readonly List<GuildPermission> KeyPermissions = new List<GuildPermission>
        {
            GuildPermission.KickMembers, GuildPermission.BanMembers, GuildPermission.ManageChannels,
            GuildPermission.ManageRoles, GuildPermission.ManageGuild, GuildPermission.ManageWebhooks,
            GuildPermission.ManageNicknames, GuildPermission.MentionEveryone, GuildPermission.ManageEmojis
        };

        private readonly IHttpClientFactory _httpClientFactory;

        public UtilityModule(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        [Command("id")]
        [Summary("Displays your Discord snowflake.")]
        public Task IdAsync()
        {
            return ReplyAsync($"ID: {Context.User.Id}");
        }

        [Command("ping")]
        [Summary("Returns the latency of the bot to Discord.")]
        public Task PingAsync()
        {
            return ReplyAsync($"Pong! {Context.Client.Latency}ms");
        }

        [Command("serverinfo")]
        [Summary("Returns the server's information")]
        [RequireContext(ContextType.Guild)]
        public async Task ServerInfoAsync()
        {
            var builder = new EmbedBuilder
            {
                Author = new EmbedAuthorBuilder
                {
                    Name = $"{Context.Guild.Owner.Username}#{Context.Guild.Owner.Discriminator}",
                    IconUrl = Context.Guild.Owner.GetAvatarUrl()
                },
                Color = new Color(114, 137, 218),
                Description = Context.Guild.Name,
                ThumbnailUrl = Context.Guild.IconUrl
            };

            var regions = await Context.Guild.GetVoiceRegionsAsync();

            var fields = new Dictionary<string, string>
            {
                {"Id", Context.Guild.Id.ToString()},
                {
                    "Region", regions.Where(e => e.Id == Context.Guild.VoiceRegionId)
                        .Select(e => e.Name)
                        .DefaultIfEmpty("unknown")
                        .FirstOrDefault()
                },
                {"Categories", Context.Guild.CategoryChannels.Count().ToString()},
                {"Text Channels", Context.Guild.TextChannels.Count().ToString()},
                {"Voice Channels", Context.Guild.VoiceChannels.Count().ToString()},
                {"Total Members", Context.Guild.MemberCount.ToString()},
                {"Online Members", Context.Guild.Users.Count(e => e.Status != UserStatus.Offline).ToString()},
                {"People", Context.Guild.Users.Count(e => !e.IsBot).ToString()},
                {"Bots", Context.Guild.Users.Count(e => e.IsBot).ToString()},
                {"Emojis", Context.Guild.Emotes.Count().ToString()},
                {"Created At", Context.Guild.CreatedAt.ToString("r")}
            };

            builder.WithFields(
                fields.Select(e => new EmbedFieldBuilder {Name = e.Key, Value = e.Value, IsInline = true}));
            await ReplyAsync("", false, builder.Build());
        }

        [Command("userinfo")]
        [Summary("Returns the user's information")]
        [RequireContext(ContextType.Guild)]
        public Task UserInfoAsync(IGuildUser user = null)
        {
            if (user == null) user = Context.Guild.GetUser(Context.User.Id);

            var color = user.Status switch
            {
                UserStatus.Idle => new Color(250, 166, 26),
                UserStatus.AFK => new Color(250, 166, 26),
                UserStatus.DoNotDisturb => new Color(240, 71, 71),
                UserStatus.Invisible => new Color(116, 127, 141),
                UserStatus.Offline => new Color(116, 127, 141),
                _ => new Color(67, 181, 129)
            };

            var builder = new EmbedBuilder
            {
                Color = color,
                Author = new EmbedAuthorBuilder
                {
                    Name = $"{user.Username}#{user.Discriminator}",
                    IconUrl = user.GetAvatarUrl()
                },
                ThumbnailUrl = user.GetAvatarUrl(),
                Description = user.Mention,
                Timestamp = DateTimeOffset.Now,
                Footer = new EmbedFooterBuilder
                {
                    Text = $"ID: {user.Id}"
                }
            };

            var roles = user.RoleIds
                .Select(id => Context.Guild.GetRole(id))
                .Where(e => !e.IsEveryone);

            var fields = new Dictionary<string, string>
            {
                {"Status", user.Status.ToString()},
                {"Joined", user.JoinedAt.HasValue ? user.JoinedAt.Value.ToString("r") : "Left Server"},
                {"Registered", user.CreatedAt.ToString("r")},
                {$"Roles [{roles.Count()}]", string.Join(" ", roles.Select(e => e.Mention))}
            };

            builder.WithFields(
                fields.Select(e => new EmbedFieldBuilder {Name = e.Key, Value = e.Value, IsInline = true}));

            if (user.GuildPermissions.Administrator)
            {
                builder.AddField("Key Permissions", GuildPermission.Administrator.ToString(), true);
            }
            else
            {
                var perms = KeyPermissions
                    .Where(e => user.GuildPermissions.Has(e))
                    .Select(e => e.ToString())
                    .ToArray();
                if (perms.Any())
                    builder.AddField("Key Permissions", string.Join(" ", perms), true);
            }

            return ReplyAsync("", false, builder.Build());
        }

        [Command("stats")]
        [Summary("Displays the bot statistics")]
        public Task<RuntimeResult> StatsAsync()
        {
            var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            var message =
                "📊📈 **Stats**\n" +
                $"🔥 **Uptime:** {uptime.ToHumanTimeString(2)}\n" +
                $"🏓 **Ping:** {Context.Client.Latency}ms\n" +
                $"🛡 **Guilds:** {Context.Client.Guilds.Count()}\n" +
                $"😊 **Total Members:** {Context.Client.Guilds.Aggregate(0, (a, g) => a + g.MemberCount)}";

            return Task.FromResult<RuntimeResult>(CommandResult.FromSuccess(message));
        }

        [Command("invite")]
        [Summary("Generates an invite link for adding the bot to your Discord")]
        public async Task InviteAsync()
        {
            var applicationInfo = await Context.Client.GetApplicationInfoAsync();
            await ReplyAsync($"{BaseUrl}?client_id={applicationInfo.Id}&permissions=8&scope=bot");
        }

        [Command("roll")]
        [Summary("Generates a number between 1 and the number specified")]
        public async Task RollAsync(int number)
        {
            await ReplyAsync($"{Context.User.Mention} rolled a {RandomNumberGenerator.GetInt32(1, number + 1)}");
        }

        [Command("jumbo")]
        [Summary("Takes an emote and makes it larger")]
        public async Task<RuntimeResult> JumboAsync(string emote)
        {
            if (!Emote.TryParse(emote, out var emoteObj))
                return CommandResult.FromError("Emote must be a guild/server emote.");

            var emoteUri = new Uri(emoteObj.Url);

            var httpClient = _httpClientFactory.CreateClient();
            var response = await httpClient.GetAsync(emoteUri);
            await using var contentStream = await response.Content.ReadAsStreamAsync();

            await Context.Channel.SendFileAsync(contentStream, Path.GetFileName(emoteUri.LocalPath));
            return CommandResult.FromSuccess("");
        }

        [Command("avatar")]
        [Summary("If specified, displays the user's avatar; else, displays your avatar")]
        public async Task AvatarAsync(IUser user = null)
        {
            if (user == null) user = Context.User;

            var avatarUri = new Uri(user.GetAvatarUrl() ?? user.GetDefaultAvatarUrl());

            var httpClient = _httpClientFactory.CreateClient();
            await using var response = await httpClient.GetStreamAsync(avatarUri);

            await Context.Channel.SendFileAsync(response, Path.GetFileName(avatarUri.LocalPath));
        }

        [Command("xkcd")]
        [Summary("Fetches an xkcd comic. Random for a random id")]
        public async Task XKCDAsync(string id = null)
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

            var embedBuilder = new EmbedBuilder
            {
                Title = $"xkcd: {data.safe_title}",
                ImageUrl = data.img,
                Footer = new EmbedFooterBuilder
                {
                    Text = data.alt
                }
            };

            await Context.Channel.SendMessageAsync(embed: embedBuilder.Build());
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