using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace LucoaBot.Commands
{
    [RequireBotPermissions(Permissions.SendMessages)]
    [ModuleLifespan(ModuleLifespan.Transient)]
    public class AdminModule : BaseCommandModule
    {
        private readonly IMemoryCache _cache;
        private readonly DatabaseContext _databaseContext;

        public AdminModule(DatabaseContext databaseContext, IMemoryCache cache)
        {
            _databaseContext = databaseContext;
            _cache = cache;
        }

        [Command("logging")]
        [Description("Sets up the logging channel for events.")]
        [RequireGuild]
        [RequireUserPermissions(Permissions.ManageGuild)]
        public async Task LogAsync(CommandContext context, DiscordChannel channel)
        {
            var config = await _databaseContext.GuildConfigs.AsQueryable()
                .Where(e => e.GuildId == context.Guild.Id)
                .FirstOrDefaultAsync();

            if (channel == null)
            {
                config.LogChannel = null;
                await context.RespondAsync("Log channel has been cleared.");
            }
            else
            {
                config.LogChannel = channel.Id;
                await context.RespondAsync($"Log channel set to {channel.Mention}");
            }

            await _databaseContext.SaveChangesAsync();
        }

        [Command("prune")]
        [Description("Deletes [num] of messages from the current channel.")]
        [RequireGuild]
        [RequireUserPermissions(Permissions.ManageMessages)]
        [RequireBotPermissions(Permissions.ManageMessages)]
        public async Task PruneAsync(CommandContext context, int num)
        {
            switch (context.Channel.Type)
            {
                case ChannelType.Text:
                {
                    var messages = await context.Channel.GetMessagesAsync(num + 1);
                    await context.Channel.DeleteMessagesAsync(messages);
                    var message = await context.RespondAsync($"Bulk deleted {num} messages.");
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    await message.DeleteAsync();
                }
                    break;
                default:
                {
                    var message = await context.RespondAsync("We can't delete messages here.");
                    await Task.Delay(TimeSpan.FromSeconds(5));

                    await message.DeleteAsync();
                }
                    break;
            }
        }

        [Command("prefix")]
        [Description("Changes the prefix for the bot.")]
        [RequireGuild]
        [RequireUserPermissions(Permissions.Administrator)]
        public async Task PrefixAsync(CommandContext context, string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 16)
            {
                await context.RespondAsync("Prefix must be between 1 and 16 characters.");
            }
            else
            {
                var config = await _databaseContext.GuildConfigs.AsQueryable()
                    .Where(e => e.GuildId == context.Guild.Id)
                    .SingleOrDefaultAsync();

                config.Prefix = prefix;
                await _databaseContext.SaveChangesAsync();

                _cache.Set("guildconfig:" + context.Guild.Id, prefix);

                await context.RespondAsync(
                    $"{context.User.Username}#{context.User.Discriminator} has changed the prefix to `{prefix}`");
            }
        }

        [Command("guilds")]
        [Description("Lists the Guilds the bot is connected to.")]
        [RequireOwner]
        public async Task GuildsAsync(CommandContext context)
        {
            var sb = new StringBuilder();
            foreach (var (id, guild) in context.Client.Guilds)
            {
                sb.Append(id);
                sb.Append(":");
                sb.AppendLine(guild.Name);
            }

            await context.RespondAsync(sb.ToString());
        }

        [Command("leaveguild")]
        [Description("Leaves a Guild")]
        [RequireOwner]
        public async Task LeaveGuildAsync(CommandContext context, DiscordGuild guild)
        {
            await guild.LeaveAsync();
            await context.RespondAsync($"Left guild {guild.Id} {guild.Name}");
        }
    }
}