using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LucoaBot.Commands
{
    // TODO: we need to add logging here and try catch around reaction processing.
    [Name("Starboard")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.ManageGuild)]
    [RequireBotPermission(ChannelPermission.SendMessages)]
    public class StarboardModule : ModuleBase<CustomContext>
    {
        private readonly DatabaseContext _context;

        public StarboardModule(DatabaseContext context)
        {
            _context = context;
        }

        [Command("starboard")]
        [Summary("Sets up the starboard")]
        public async Task SetStarboardAsync(SocketTextChannel channel = null)
        {
            var config = await _context.GuildConfigs.AsQueryable()
                .Where(e => e.GuildId == Context.Guild.Id)
                .FirstOrDefaultAsync();

            if (channel == null)
            {
                config.StarBoardChannel = null;
                await ReplyAsync("Starboard has been cleared.");
            }
            else
            {
                config.StarBoardChannel = channel.Id;
                await ReplyAsync($"Starboard set to {channel.Mention}");
            }

            await _context.SaveChangesAsync();
        }
    }
}