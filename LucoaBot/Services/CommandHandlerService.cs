using Discord.Commands;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Prometheus;
using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace LucoaBot.Services
{
    public class CommandHandlerService
    {
        private readonly IServiceProvider services;
        private readonly DiscordSocketClient client;
        private readonly CommandService commands;
        private readonly DatabaseContext context;

        // this is a bit of a hack to put it in here... should move to it's own handler later
        private static readonly Counter messageSeenCount = Metrics.CreateCounter("discord_messages_total", "Total messages seen count");
        private static readonly Counter commandCount = Metrics.CreateCounter("discord_command_count", "Executed commands", labelNames: new[] { "result" });

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

        private async Task OnCommandExecutedAsync(Discord.Optional<CommandInfo> command, ICommandContext cmdContext, IResult result)
        {
            if (result != null)
            {
                if (!result.IsSuccess)
                {
                    // Unknown errors are to be ignored for failure count
                    if (result.Error != CommandError.UnknownCommand)
                        commandCount.WithLabels("failure").Inc();

                    switch (result.Error)
                    {
                        case CommandError.Exception:
                            return;
                        case CommandError.UnknownCommand:
                            if (cmdContext is CustomCommandContext) // this should always be true, TODO: figure out a better way to remove this
                            {
                                var customContext = cmdContext as CustomCommandContext;
                                Task.Run(async () =>
                                {
                                    var commandKey = customContext.Message.Content.Substring(customContext.ArgPos).Trim().ToLowerInvariant();
                                    var command = await context.CustomCommands
                                        .Where(c => c.Command == commandKey)
                                        .FirstOrDefaultAsync();

                                    if (command != null)
                                    {
                                        commandCount.WithLabels("success").Inc();
                                        // filter out @everyone and @here mentions...
                                        var response = command.Response
                                            .Replace("@everyone", "@\u0435veryone")
                                            .Replace("@here", "@h\u0435re");
                                        await customContext.Channel.SendMessageAsync(response);
                                    }
                                }).SafeFireAndForget(false);
                            }
                            return;
                    }
                }
                else // this is a successful command
                {
                    commandCount.WithLabels("success").Inc();
                }
                
                if (!string.IsNullOrEmpty(result.ErrorReason))
                {
                    await cmdContext.Channel.SendMessageAsync(result.ErrorReason);
                }
            }
        }

        public async Task HandleCommandAsync(SocketMessage socketMessage)
        {
            messageSeenCount.Inc();

            // don't process system or bot messages
            var message = socketMessage as SocketUserMessage;
            if (message == null || message.Author.IsBot) return;

            var prefix = ".";

            var _context = new CustomCommandContext(client, message);

            if (!_context.IsPrivate)
            {
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

                _context.Config = config;

                prefix = config.Prefix;
            }

            int argPos = 0;
            if (!message.HasStringPrefix(prefix, ref argPos))
                return;

            _context.ArgPos = argPos;

            await commands.ExecuteAsync(_context, argPos, services);
        }
    }
}
