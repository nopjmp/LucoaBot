using System;
using System.Linq;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using LucoaBot.Commands;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace LucoaBot.Services
{
    public class CommandHandlerService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<CommandHandlerService> _logger;

        public readonly CommandsNextExtension Commands;

        public CommandHandlerService(IServiceProvider services, DiscordClient client,
            ILogger<CommandHandlerService> logger)
        {
            _services = services;
            _logger = logger;

            Commands = client.UseCommandsNext(new CommandsNextConfiguration()
            {
                Services = _services,
                PrefixResolver = PrefixResolver
            });

            Commands.RegisterCommands<AdminModule>();
            Commands.RegisterCommands<CustomCommandModule>();
            Commands.RegisterCommands<SearchModule>();
            Commands.RegisterCommands<SelfRoleModule>();
            Commands.RegisterCommands<StarboardModule>();
            Commands.RegisterCommands<UtilityModule>();

            Commands.CommandErrored += CommandsOnCommandErrored;
        }

        private async Task<int> PrefixResolver(DiscordMessage msg)
        {
            var prefix = ".";
            using var scope = _services.CreateScope();
            var databaseContext = scope.ServiceProvider.GetService<DatabaseContext>();

            var guildConfigPrefix = await databaseContext.GuildConfigs.AsNoTracking()
                .Where(e => e.GuildId == msg.Channel.GuildId)
                .Select(e => e.Prefix)
                .FirstOrDefaultAsync();

            return msg.GetStringPrefixLength(guildConfigPrefix ?? prefix);
        }

        private async Task CommandsOnCommandErrored(CommandErrorEventArgs args)
        {
            switch (args.Exception)
            {
                case CommandNotFoundException e:
                {
                    using var scope = _services.CreateScope();
                    var context = scope.ServiceProvider.GetService<DatabaseContext>();
                    var customCommand = await context.CustomCommands.AsNoTracking()
                        .Where(c => c.Command == e.CommandName)
                        .Select(c => c.Response)
                        .FirstOrDefaultAsync();

                    if (customCommand != null)
                    {
                        // filter out @everyone and @here mentions...
                        var response = customCommand
                            // ReSharper disable once StringLiteralTypo
                            .Replace("@everyone", "@\u0435veryone")
                            .Replace("@here", "@h\u0435re");
                        await args.Context.RespondAsync(response);
                    }

                    break;
                }
                default:
                    _logger.Log(LogLevel.Warning, args.Exception, "Exception occured while processing command.");
                    break;
            }
        }
    }
}