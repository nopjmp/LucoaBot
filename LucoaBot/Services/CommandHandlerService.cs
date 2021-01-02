using System;
using System.Collections.Generic;
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
        private readonly ILogger<CommandHandlerService> _logger;
        private readonly IServiceProvider _services;

        public readonly CommandsNextExtension Commands;

        public CommandHandlerService(IServiceProvider services, DiscordClient client,
            ILogger<CommandHandlerService> logger)
        {
            _services = services;
            _logger = logger;

            Commands = client.UseCommandsNext(new CommandsNextConfiguration
            {
                Services = _services,
                PrefixResolver = PrefixResolver,
                EnableMentionPrefix = false,
            });

            Commands.RegisterCommands<AdminModule>();
            Commands.RegisterCommands<CustomCommandModule>();
            Commands.RegisterCommands<SearchModule>();
            Commands.RegisterCommands<SelfRoleModule>();
            Commands.RegisterCommands<StarboardModule>();
            Commands.RegisterCommands<UtilityModule>();

            Commands.CommandErrored += (_, e) => 
            {
                CommandsOnCommandErrored(e).Forget();
                return Task.CompletedTask;
            };
        }

        private async Task<int> PrefixResolver(DiscordMessage msg)
        {
            var prefix = ".";
            using var scope = _services.CreateScope();
            var databaseContext = scope.ServiceProvider.GetService<DatabaseContext>();

            if (databaseContext == null)
                return msg.GetStringPrefixLength(prefix);

            var guildConfigPrefix = await databaseContext.GuildConfigs.AsNoTracking()
                .Where(e => e.GuildId == msg.Channel.GuildId)
                .Select(e => e.Prefix)
                .FirstOrDefaultAsync();

            return msg.GetStringPrefixLength(guildConfigPrefix ?? prefix);
        }

        private async Task CommandsOnCommandErrored(CommandErrorEventArgs args)
        {
            var exs = new List<Exception>();
            if (args.Exception is AggregateException ae)
                exs.AddRange(ae.InnerExceptions);
            else
                exs.Add(args.Exception);

            foreach (var ex in exs)
            {
                if (ex is CommandNotFoundException e && args.Command == null)
                {
                    using var scope = _services.CreateScope();
                    var context = scope.ServiceProvider.GetService<DatabaseContext>();

                    if (context != null)
                    {
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
                    }

                    break;
                }

                _logger.Log(LogLevel.Warning, ex,
                    "Exception occured while processing command ({0}) from {1} with message \"{2}\".\n",
                    args.Command.QualifiedName, args.Context.User.ToString(), args.Context.Message.Content);
            }
        }
    }
}