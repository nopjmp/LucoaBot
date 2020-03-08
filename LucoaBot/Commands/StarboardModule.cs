﻿using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using DSharpPlus.Entities;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LucoaBot.Commands
{
    // TODO: we need to add logging here and try catch around reaction processing.
    [RequireGuild]
    [RequireUserPermissions(Permissions.ManageGuild)]
    [RequireBotPermissions(Permissions.SendMessages)]
    [ModuleLifespan(ModuleLifespan.Transient)]
    public class StarboardModule : BaseCommandModule
    {
        private readonly DatabaseContext _databaseContext;

        public StarboardModule(DatabaseContext databaseContext)
        {
            _databaseContext = databaseContext;
        }

        [Command("starboard")]
        [Description("Sets up the starboard")]
        public async Task SetStarboardAsync(CommandContext context, DiscordChannel channel = null)
        {
            var config = await _databaseContext.GuildConfigs.AsQueryable()
                .Where(e => e.GuildId == context.Guild.Id)
                .FirstOrDefaultAsync();

            if (channel == null)
            {
                config.StarBoardChannel = null;
                await context.RespondAsync("Starboard has been cleared.");
            }
            else
            {
                config.StarBoardChannel = channel.Id;
                await context.RespondAsync($"Starboard set to {channel.Mention}");
            }

            await _databaseContext.SaveChangesAsync();
        }
    }
}