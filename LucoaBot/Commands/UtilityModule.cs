﻿using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LucoaBot.Commands
{
    [Name("Utility")]
    public class UtilityModule : ModuleBase<SocketCommandContext>
    {
        private readonly List<GuildPermission> keyPermissions = new List<GuildPermission>()
        {
            GuildPermission.KickMembers, GuildPermission.BanMembers, GuildPermission.ManageChannels,
            GuildPermission.ManageRoles, GuildPermission.ManageGuild, GuildPermission.ManageWebhooks,
            GuildPermission.ManageNicknames, GuildPermission.MentionEveryone, GuildPermission.ManageEmojis
        };

        [Command("id")]
        [Summary("Displays your Discord snowflake.")]
        public Task IdAsync()
        {
            ReplyAsync($"ID: {Context.User.Id}").SafeFireAndForget(false);
            return Task.CompletedTask;
        }

        [Command("ping")]
        [Summary("Returns the latency of the bot to Discord.")]
        public Task PingAsync()
        {
            ReplyAsync($"Pong! {Context.Client.Latency}ms").SafeFireAndForget(false);
            return Task.CompletedTask;
        }

        [Command("serverinfo")]
        [Summary("Returns the server's information")]
        public async Task ServerInfoAsync()
        {
            var builder = new EmbedBuilder()
            {
                Author = new EmbedAuthorBuilder()
                {
                    Name = $"{Context.Guild.Owner.Username}#{Context.Guild.Owner.Discriminator}",
                    IconUrl = Context.Guild.Owner.GetAvatarUrl()
                },
                Color = new Color(114, 137, 218),
                Description = Context.Guild.Name,
                ThumbnailUrl = Context.Guild.IconUrl
            };

            var regions = await Context.Guild.GetVoiceRegionsAsync();

            var fields = new Dictionary<string, string>()
            {
                { "Id", Context.Guild.Id.ToString() },
                { "Region", regions.Where(e => e.Id == Context.Guild.VoiceRegionId)
                    .Select(e => e.Name)
                    .DefaultIfEmpty("unknown")
                    .FirstOrDefault() },
                { "Categories", Context.Guild.CategoryChannels.Count().ToString() },
                { "Text Channels", Context.Guild.TextChannels.Count().ToString() },
                { "Voice Channels", Context.Guild.VoiceChannels.Count().ToString() },
                { "Total Members", Context.Guild.MemberCount.ToString() },
                { "Online Members", Context.Guild.Users.Count(e => e.Status != UserStatus.Offline).ToString() },
                { "People", Context.Guild.Users.Count(e => !e.IsBot).ToString() },
                { "Bots", Context.Guild.Users.Count(e => e.IsBot).ToString() },
                { "Emojis", Context.Guild.Emotes.Count().ToString() },
                { "Created At", Context.Guild.CreatedAt.ToString("r") }
            };

            builder.WithFields(fields.Select(e => new EmbedFieldBuilder() { Name = e.Key, Value = e.Value, IsInline = true }));
            ReplyAsync("", false, builder.Build()).SafeFireAndForget();
        }

        [Command("userinfo")]
        [Summary("Returns the user's information")]
        public Task UserInfoAsync(IGuildUser user = null)
        {
            if (user == null)
            {
                user = Context.Guild.GetUser(Context.User.Id);
            }

            var color = new Color(67, 181, 129);
            switch (user.Status)
            {
                case UserStatus.Idle:
                case UserStatus.AFK:
                    color = new Color(250, 166, 26);
                    break;
                case UserStatus.DoNotDisturb:
                    color = new Color(240, 71, 71);
                    break;
                case UserStatus.Invisible:
                case UserStatus.Offline:
                    color = new Color(116, 127, 141);
                    break;
            }

            var builder = new EmbedBuilder()
            {
                Color = color,
                Author = new EmbedAuthorBuilder()
                {
                    Name = $"{user.Username}#{user.Discriminator}",
                    IconUrl = user.GetAvatarUrl(),
                },
                ThumbnailUrl = user.GetAvatarUrl(),
                Description = user.Mention,
                Timestamp = DateTimeOffset.Now,
                Footer = new EmbedFooterBuilder()
                {
                    Text = $"ID: {user.Id}",
                }
            };

            var roles = user.RoleIds
                .Select(id => Context.Guild.GetRole(id))
                .Where(e => !e.IsEveryone);

            var fields = new Dictionary<string, string>()
            {
                { "Status", user.Status.ToString() },
                { "Joined", user.JoinedAt.HasValue ? user.JoinedAt.Value.ToString("r") : "Left Server" },
                { "Registered", user.CreatedAt.ToString("r") },
                { $"Roles [{roles.Count()}]", string.Join(" ", roles.Select(e => e.Mention)) }
            };

            builder.WithFields(fields.Select(e => new EmbedFieldBuilder() { Name = e.Key, Value = e.Value, IsInline = true }));

            if (user.GuildPermissions.Administrator)
            {
                builder.AddField("Key Permissions", GuildPermission.Administrator.ToString(), true);
            }
            else
            {
                var perms = keyPermissions
                    .Where(e => user.GuildPermissions.Has(e))
                    .Select(e => e.ToString());
                if (perms.Count() > 0)
                    builder.AddField("Key Permissions", string.Join(" ", perms), true);
            }

            ReplyAsync("", false, builder.Build()).SafeFireAndForget(false);
            return Task.CompletedTask;
        }

        [Command("stats")]
        [Summary("Displays the bot statistics")]
        public async Task StatsAsync()
        {
            var sb = new StringBuilder();

            sb.Append("📊📈 **Stats**\n");

            var uptime = DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime();
            sb.Append("🔥 **Uptime:** ").Append(uptime.ToHumanTimeString(2));

            await ReplyAsync(sb.ToString());
        }

        const string baseUrl = "https://discordapp.com/api/oauth2/authorize";

        [Command("invite")]
        [Summary("Generates an invite link for adding the bot to your Discord")]
        public async Task InviteAsync()
        {
            var applicationInfo = await Context.Client.GetApplicationInfoAsync();
            ReplyAsync($"{baseUrl}?client_id={applicationInfo.Id}&permissions=8&scope=bot").SafeFireAndForget(false);
        }
    }
}