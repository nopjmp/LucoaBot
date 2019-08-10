﻿using Discord;
using Discord.Commands;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace LucoaBot.Commands
{
    [Name("Admin")]
    [RequireContext(ContextType.Guild)]
    public class AdminModule : ModuleBase<SocketCommandContext>
    {
        private readonly DatabaseContext _context;

        public AdminModule(DatabaseContext context)
        {
            _context = context;
        }

        [Command("prune")]
        [Summary("Deletes [num] of messages from the current channel.")]
        [RequireUserPermission(GuildPermission.ManageMessages)]
        [RequireBotPermission(GuildPermission.ManageMessages)]
        public async Task PruneAsync(int num)
        {
            switch (Context.Channel)
            {
                case ITextChannel c:
                    {
                        var messages = await c.GetMessagesAsync(num + 1).FlattenAsync();
                        c.DeleteMessagesAsync(messages).SafeFireAndForget(false);
                        var message = await ReplyAsync($"Bulk deleted {num} messages.");
                        await Task.Delay(TimeSpan.FromSeconds(5));

                        message.DeleteAsync().SafeFireAndForget(false);
                    }
                    break;
                default:
                    {
                        var message = await ReplyAsync("We can't delete messages here.");
                        await Task.Delay(TimeSpan.FromSeconds(5));

                        message.DeleteAsync().SafeFireAndForget(false);
                    }
                    break;
            }
        }

        [Command("prefix")]
        [Summary("Changes the prefix for the bot.")]
        [RequireUserPermission(GuildPermission.Administrator)]
        public async Task PrefixAsync(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix) || prefix.Length > 16)
            {
                ReplyAsync("Prefix must be between 1 and 16 characters.").SafeFireAndForget(false);
            }
            else
            {
                var config = await _context.GuildConfigs
                    .Where(e => e.GuildId == Context.Guild.Id)
                    .SingleOrDefaultAsync();

                config.Prefix = prefix;
                _context.SaveChangesAsync().SafeFireAndForget(false);

                ReplyAsync($"{Context.User.Username}#{Context.User.Discriminator} has changed the prefix to `{prefix}`").SafeFireAndForget(false);
            }
        }
    }
}
