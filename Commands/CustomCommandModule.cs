using System.Linq;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using LucoaBot.Models;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LucoaBot.Commands
{
    [Name("CustomCommands")]
    [RequireContext(ContextType.Guild)]
    [RequireUserPermission(GuildPermission.ManageMessages)]
    [RequireBotPermission(ChannelPermission.SendMessages)]
    public class CustomCommandModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _commandService;
        private readonly DatabaseContext _database;

        public CustomCommandModule(DatabaseContext database, CommandService commandService)
        {
            _database = database;
            _commandService = commandService;
        }

        [Command("addcommand")]
        [Alias("ac")]
        public async Task<RuntimeResult> AddCommandAsync(string command, params string[] response)
        {
            var commandKey = command.ToLowerInvariant();

            if (_commandService.Commands.Any(c => c.Name == commandKey || c.Aliases.Any(a => a == commandKey)))
                return CommandResult.FromError("You cannot use a command that already exists as a bot command.");

            var status = "Updated";

            var entry = await _database.CustomCommands.AsQueryable()
                .Where(c => c.Command == commandKey)
                .FirstOrDefaultAsync();

            if (entry == null)
            {
                status = "Added";
                entry = new CustomCommand
                {
                    Command = commandKey,
                    GuildId = Context.Guild.Id
                };

                _database.CustomCommands.Add(entry);
            }

            entry.Response = string.Join(" ", response.Select(s => s.Contains(" ") ? $"\"{s}\"" : s));

            await _database.SaveChangesAsync();

            return CommandResult.FromSuccess($"{status} command `{commandKey}`.");
        }

        [Command("removecommand")]
        [Alias("rc")]
        public async Task<RuntimeResult> RemoveCommandAsync(string command)
        {
            var commandKey = command.ToLowerInvariant();

            var entry = await _database.CustomCommands.AsQueryable()
                .Where(c => c.Command == commandKey)
                .FirstOrDefaultAsync();

            if (entry == null)
                return CommandResult.FromError("You can only delete custom commands that exist.");

            _database.Remove(entry);
            await _database.SaveChangesAsync();

            return CommandResult.FromSuccess($"Deleted command `{commandKey}`.");
        }
    }
}