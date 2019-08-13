using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace LucoaBot.Services
{
    internal class CommandHandlerService
    {
        private readonly IServiceProvider services;
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly DatabaseContext context;

        public CommandHandlerService(IServiceProvider services, DiscordSocketClient client, CommandService commands, DatabaseContext context)
        {
            this.services = services;
            this.client = client;
            this.commands = commands;
            this.context = context;
        }

        public async Task InitializeAsync()
        {
            // Pass the service provider to the second parameter of
            // AddModulesAsync to inject dependencies to all modules 
            // that may require them.
            await commands.AddModulesAsync(
                assembly: Assembly.GetEntryAssembly(),
                services: services);

            commands.CommandExecuted += OnCommandExecutedAsync;

            client.MessageReceived += HandleCommandAsync;
        }

        private async Task OnCommandExecutedAsync(Discord.Optional<CommandInfo> command, ICommandContext context, IResult result)
        {
            if (result != null && !result.IsSuccess)
            {
                switch (result.Error)
                {
                    case CommandError.Exception:
                    case CommandError.UnknownCommand:
                        return;
                }

                if (!string.IsNullOrEmpty(result.ErrorReason))
                {
                    await context.Channel.SendMessageAsync(result.ErrorReason);
                }
            }
        }

        public async Task HandleCommandAsync(SocketMessage socketMessage)
        {
            // don't process system or bot messages
            var message = socketMessage as SocketUserMessage;
            if (message == null || message.Author.IsBot) return;

            var _context = new SocketCommandContext(client, message);

            var config = await context.GuildConfigs
                .Where(e => e.GuildId == _context.Guild.Id)
                .FirstOrDefaultAsync();

            if (config == null)
            {
                config = new Models.GuildConfig()
                {
                    GuildId = _context.Guild.Id,
                    Prefix = "."
                };

                context.Add(config);
                context.SaveChangesAsync().SafeFireAndForget(false);
            }

            int argPos = 0;
            if (!message.HasStringPrefix(config.Prefix, ref argPos))
                return;

            await commands.ExecuteAsync(_context, argPos, services);

            var _ = Task.Run(async () =>
            {
                var commandKey = message.Content.Substring(argPos).Trim().ToLowerInvariant();
                if (!commands.Commands.Any(c => c.Name == commandKey || c.Aliases.Any(a => a == commandKey)))
                {
                    var command = await context.CustomCommands
                        .Where(c => c.Command == commandKey)
                        .FirstOrDefaultAsync();

                    if (command != null)
                    {
                        // filter out @everyone and @here mentions...
                        var response = command.Response
                            .Replace("@everyone", "@\u0435veryone")
                            .Replace("@here", "@h\u0435re");
                        await _context.Channel.SendMessageAsync(response);
                    }
                }
            });
        }
    }
}
