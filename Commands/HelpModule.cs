using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;

namespace LucoaBot.Commands
{
    [Name("Help")]
    [RequireBotPermission(ChannelPermission.SendMessages)]
    public class HelpModule : ModuleBase<CustomContext>
    {
        private readonly DatabaseContext _context;
        private readonly CommandService _service;

        public HelpModule(CommandService service, DatabaseContext context)
        {
            _service = service;
            _context = context;
        }

        [Command("help")]
        [Summary("Returns a help message to display what commands are available.")]
        public async Task HelpAsync()
        {
            var prefix = ".";

            if (!Context.IsPrivate)
            {
                var config = await _context.GuildConfigs.AsNoTracking()
                    .Where(e => e.GuildId == Context.Guild.Id)
                    .FirstOrDefaultAsync();

                if (config != null) prefix = config.Prefix;
            }

            var builder = new EmbedBuilder
            {
                Color = new Color(114, 137, 218),
                Description = "Bot Commands"
            };

            var stringBuilder = new StringBuilder();

            foreach (var module in _service.Modules)
            {
                stringBuilder.Clear();
                foreach (var cmd in module.Commands)
                {
                    var result = await cmd.CheckPreconditionsAsync(Context);
                    if (!result.IsSuccess) continue;
                    
                    stringBuilder.Append($"{prefix}{cmd.Name} ");
                    stringBuilder.AppendJoin(" ", cmd.Parameters.Select(
                        p => p.IsOptional ? $"[{p.Name}]" : p.Name));
                    if (!string.IsNullOrWhiteSpace(cmd.Summary))
                        stringBuilder.Append($" - {cmd.Summary}");
                    stringBuilder.Append("\n");

                    if (cmd.Aliases.Count <= 1) continue;
                    
                    stringBuilder.Append("\u2001*");
                    stringBuilder.Append(cmd.Aliases.Count > 2 ? "Aliases: " : "Alias: ");
                    stringBuilder.AppendJoin(", ", cmd.Aliases.Skip(1));
                    stringBuilder.Append("*\n");
                }

                if (stringBuilder.Length > 0)
                    builder.AddField(f =>
                    {
                        f.Name = module.Name;
                        f.Value = stringBuilder.ToString();
                        f.IsInline = false;
                    });
            }

            await ReplyAsync("", false, builder.Build());
        }
    }
}