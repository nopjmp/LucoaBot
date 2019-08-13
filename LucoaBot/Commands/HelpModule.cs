using Discord;
using Discord.Commands;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LucoaBot.Commands
{
    [Name("Help")]
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _service;
        private readonly DatabaseContext _context;

        public HelpModule(CommandService service, DatabaseContext context)
        {
            _service = service;
            _context = context;
        }

        [Command("help")]
        [Summary("Returns a help message to display what commands are available.")]
        public async Task HelpAsync()
        {
            var config = await _context.GuildConfigs
                .Where(e => e.GuildId == Context.Guild.Id)
                .FirstOrDefaultAsync();

            var builder = new EmbedBuilder()
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
                    if (result.IsSuccess)
                    {
                        stringBuilder.Append($"{config.Prefix}{cmd.Name} ");
                        stringBuilder.AppendJoin(" ", cmd.Parameters.Select(
                            p =>
                            {
                                if (p.IsOptional)
                                    return $"[{p.Name}]";
                                return p.Name;
                            }));
                        if (!string.IsNullOrWhiteSpace(cmd.Summary))
                        {
                            stringBuilder.Append($" - {cmd.Summary}");
                        }
                        stringBuilder.Append("\n");
                    }
                }

                if (stringBuilder.Length > 0)
                {
                    builder.AddField(f =>
                    {
                        f.Name = module.Name;
                        f.Value = stringBuilder.ToString();
                        f.IsInline = false;
                    });
                }
            }

            ReplyAsync("", false, builder.Build()).SafeFireAndForget(false);
        }
    }
}
