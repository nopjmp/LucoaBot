using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Attributes;
using LucoaBot.Models;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LucoaBot.Commands
{
    [RequireGuild]
    [RequireUserPermissions(Permissions.ManageMessages)]
    [RequireBotPermissions(Permissions.SendMessages)]
    [ModuleLifespan(ModuleLifespan.Transient)]
    public class CustomCommandModule : BaseCommandModule
    {
        private readonly CommandsNextExtension _commandService;
        private readonly DatabaseContext _database;

        public CustomCommandModule(DatabaseContext database, CommandHandlerService commandService)
        {
            _database = database;
            _commandService = commandService.Commands;
        }

        [Command("addcommand")]
        [Aliases("ac")]
        [Description("adds/overwrites a custom command")]
        public async Task AddCommandAsync(CommandContext context, string command, params string[] response)
        {
            var commandKey = command.ToLowerInvariant();

            if (_commandService.RegisteredCommands.Any(c =>
                c.Key == commandKey || c.Value.Aliases.Any(a => a == commandKey)))
            {
                await context.RespondAsync("You cannot use a command that already exists as a bot command.");
                return;
            }

            var status = "Updated";

            var entry = await _database.CustomCommands
                .Where(c => c.Command == commandKey)
                .FirstOrDefaultAsync();

            if (entry == null)
            {
                status = "Added";
                entry = new CustomCommand
                {
                    Command = commandKey,
                    GuildId = context.Guild.Id
                };

                await _database.CustomCommands.AddAsync(entry);
            }

            entry.Response = string.Join(" ", response.Select(s => s.Contains(" ") ? $"\"{s}\"" : s));

            await _database.SaveChangesAsync();

            await context.RespondAsync($"{status} command `{commandKey}`.");
        }

        [Command("removecommand")]
        [Aliases("rc")]
        [Description("removes a custom command")]
        public async Task RemoveCommandAsync(CommandContext context, string command)
        {
            var commandKey = command.ToLowerInvariant();

            var entry = await _database.CustomCommands
                .Where(c => c.Command == commandKey)
                .FirstOrDefaultAsync();

            if (entry == null)
            {
                await context.RespondAsync("You can only delete custom commands that exist.");
                return;
            }

            _database.Remove(entry);
            await _database.SaveChangesAsync();

            await context.RespondAsync($"Deleted command `{commandKey}`.");
        }
    }
}