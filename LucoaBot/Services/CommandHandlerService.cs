using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.CommandsNext;
using DSharpPlus.CommandsNext.Exceptions;
using DSharpPlus.Entities;
using LucoaBot.Commands;
using LucoaBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prometheus;

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

            var guildConfig = await databaseContext.GuildConfigs.AsNoTracking()
                .Where(e => e.GuildId == msg.Channel.GuildId)
                .FirstOrDefaultAsync();

            if (guildConfig != null) prefix = guildConfig.Prefix;
            return msg.GetStringPrefixLength(prefix);
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
                        .FirstOrDefaultAsync();

                    if (customCommand != null)
                    {
                        // filter out @everyone and @here mentions...
                        var response = customCommand.Response
                            // ReSharper disable once StringLiteralTypo
                            .Replace("@everyone", "@\u0435veryone")
                            .Replace("@here", "@h\u0435re");
                        await args.Context.RespondAsync(response);
                    }

                    break;
                }
            }
        }
    }
}