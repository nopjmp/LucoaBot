﻿using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LucoaBot.Commands
{
    [Name("Admin")]
    [RequireBotPermission(ChannelPermission.SendMessages)]
    public class AdminModule : ModuleBase<CustomContext>
    {
        private readonly IMemoryCache _cache;
        private readonly DatabaseContext _context;

        public AdminModule(DatabaseContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        [Command("logging")]
        [Summary("Sets up the logging channel for events.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task LogAsync(SocketTextChannel channel)
        {
            var config = await _context.GuildConfigs.AsQueryable()
                .Where(e => e.GuildId == Context.Guild.Id)
                .FirstOrDefaultAsync();

            if (channel == null)
            {
                config.LogChannel = null;
                await ReplyAsync("Log channel has been cleared.");
            }
            else
            {
                config.LogChannel = channel.Id;
                await ReplyAsync($"Log channel set to {channel.Mention}");
            }

            await _context.SaveChangesAsync();
        }

        [Command("prune")]
        [Summary("Deletes [num] of messages from the current channel.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task PruneAsync(int num)
        {
            switch (Context.Channel)
            {
                case ITextChannel c:
                {
                    var messages = await c.GetMessagesAsync(num + 1).FlattenAsync();
                    await c.DeleteMessagesAsync(messages);
                    var message = await ReplyAsync($"Bulk deleted {num} messages.");
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    await message.DeleteAsync();
                }
                    break;
                default:
                {
                    var message = await ReplyAsync("We can't delete messages here.");
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    await message.DeleteAsync();
                }
                    break;
            }
        }

        [Command("prefix")]
        [Summary("Changes the prefix for the bot.")]
        [RequireContext(ContextType.Guild)]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task PrefixAsync(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 16)
            {
                await ReplyAsync("Prefix must be between 1 and 16 characters.");
            }
            else
            {
                var config = await _context.GuildConfigs.AsQueryable()
                    .Where(e => e.GuildId == Context.Guild.Id)
                    .SingleOrDefaultAsync();

                config.Prefix = prefix;
                await _context.SaveChangesAsync();

                _cache.Set("guildconfig:" + Context.Guild.Id, prefix);

                await ReplyAsync(
                    $"{Context.User.Username}#{Context.User.Discriminator} has changed the prefix to `{prefix}`");
            }
        }

        [Command("guilds")]
        [Summary("Lists the Guilds the bot is connected to.")]
        [RequireOwner]
        public async Task GuildsAsync()
        {
            var sb = new StringBuilder();
            foreach (var guild in Context.Client.Guilds)
            {
                sb.Append(guild.Id);
                sb.Append(":");
                sb.AppendLine(guild.Name);
            }

            await ReplyAsync(sb.ToString());
        }

        [Command("leaveguild")]
        [Summary("Leaves a Guild")]
        [RequireOwner]
        public async Task LeaveGuildAsync(ulong guildId)
        {
            var guild = Context.Client.GetGuild(guildId);
            if (guild != null)
            {
                await guild.LeaveAsync();
                await ReplyAsync($"Left guild {guildId} {guild.Name}");
            }
        }
    }
}