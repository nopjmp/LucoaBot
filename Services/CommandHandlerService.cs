using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LucoaBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace LucoaBot.Services
{
    public class CommandHandlerService
    {
        // this is a bit of a hack to put it in here... should move to it's own handler later
        private static readonly Counter MessageSeenCount =
            Metrics.CreateCounter("discord_messages_total", "Total messages seen count");

        private static readonly Counter CommandCount =
            Metrics.CreateCounter("discord_command_count", "Executed commands", "result");

        private readonly IMemoryCache _cache;
        private readonly DiscordSocketClient _client;
        private readonly CommandService _commands;
        private readonly IServiceProvider _services;
        private readonly ILogger<CommandHandlerService> _logger;
        
        public CommandHandlerService(IServiceProvider services, DiscordSocketClient client, CommandService commands, 
            IMemoryCache cache, ILogger<CommandHandlerService> logger)
        {
            _services = services;
            _client = client;
            _commands = commands;
            _cache = cache;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            // Pass the service provider to the second parameter of
            // AddModulesAsync to inject dependencies to all modules 
            // that may require them.
            await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

            _commands.CommandExecuted += OnCommandExecutedAsync;
            _client.MessageReceived += HandleCommandAsync;
        }

        private async Task OnCommandExecutedAsync(Optional<CommandInfo> command, ICommandContext cmdContext,
            IResult result)
        {
            if (result == null) return;
            
            if (result.IsSuccess)
                CommandCount.WithLabels("success").Inc();
            if (!result.IsSuccess && result.Error != CommandError.UnknownCommand)
                CommandCount.WithLabels("failure").Inc();
            
            if (cmdContext.Guild != null)
            {
                // TODO: make universal error reporting system
                var guildUser = await cmdContext.Guild.GetCurrentUserAsync();
                if (!guildUser.GuildPermissions.Has(GuildPermission.SendMessages))
                {
                    var user = cmdContext.User;
                    var channel = cmdContext.Channel;
                    var guild = cmdContext.Guild;
                    var message = cmdContext.Message;
                    var owner = await guild.GetOwnerAsync();
                    _logger.LogWarning(
                        $"Missing Guild Permission {guild.Id}:{guild.Name} {user.Username}#{user.Discriminator} {channel.Name}: {message.Content}\n" +
                        $"Owner {owner.Username}#{owner.Discriminator})");
                    return;
                }

                ChannelPermissions perms;
                if (cmdContext.Channel is IGuildChannel guildChannel)
                    perms = guildUser.GetPermissions(guildChannel);
                else
                    perms = ChannelPermissions.All(cmdContext.Channel);

                if (!perms.Has(ChannelPermission.SendMessages))
                {
                    var user = cmdContext.User;
                    var channel = cmdContext.Channel;
                    var guild = cmdContext.Guild;
                    var message = cmdContext.Message;
                    var owner = await guild.GetOwnerAsync();
                    _logger.LogWarning(
                        $"Missing Channel Permission {guild.Id}:{guild.Name} {user.Username}#{user.Discriminator} {channel.Name}: {message.Content}\n" +
                        $"Owner {owner.Username}#{owner.Discriminator})");
                    return;
                }
            }

            if (!result.IsSuccess)
            {
                // Unknown errors are to be ignored for failure count
                if (result.Error != CommandError.UnknownCommand)
                    CommandCount.WithLabels("failure").Inc();

                switch (result.Error)
                {
                    case CommandError.Exception:
                        return;
                    case CommandError.UnknownCommand:
                    {
                        // this should always be true, TODO: figure out a better way to remove this
                        if (cmdContext is CustomCommandContext customContext)
                            Task.Run(async () =>
                            {
                                var commandKey = customContext.Message.Content.Substring(customContext.ArgPos).Trim()
                                    .ToLowerInvariant();

                                using var scope = _services.CreateScope();
                                var context = scope.ServiceProvider.GetService<DatabaseContext>();
                                var customCommand = await context.CustomCommands.AsNoTracking()
                                    .Where(c => c.Command == commandKey)
                                    .FirstOrDefaultAsync();

                                if (customCommand != null)
                                {
                                    CommandCount.WithLabels("success").Inc();
                                    // filter out @everyone and @here mentions...
                                    var response = customCommand.Response
                                        // ReSharper disable once StringLiteralTypo
                                        .Replace("@everyone", "@\u0435veryone")
                                        .Replace("@here", "@h\u0435re");
                                    await customContext.Channel.SendMessageAsync(response);
                                }
                            }).SafeFireAndForget(false);

                        return;
                    }
                }
            }

            if (!string.IsNullOrEmpty(result.ErrorReason))
                await cmdContext.Channel.SendMessageAsync(result.ErrorReason);
        }

        public async Task HandleCommandAsync(SocketMessage socketMessage)
        {
            MessageSeenCount.Inc();

            // don't process system or bot messages
            if (!(socketMessage is SocketUserMessage message) || message.Author.IsBot) return;

            var prefix = ".";

            var commandContext = new CustomCommandContext(_client, message);

            if (!commandContext.IsPrivate)
            {
                // we only want to cache the prefix to keep memory low
                prefix = await _cache.GetOrCreateAsync("guildconfig:" + commandContext.Guild.Id, async entry =>
                {
                    using var scope = _services.CreateScope();
                    var databaseContext = scope.ServiceProvider.GetService<DatabaseContext>();
                    
                    var guildConfig = await databaseContext.GuildConfigs.AsNoTracking()
                        .Where(e => e.GuildId == commandContext.Guild.Id)
                        .FirstOrDefaultAsync();

                    if (guildConfig != null) return guildConfig.Prefix;
                    
                    guildConfig = new GuildConfig
                    {
                        GuildId = commandContext.Guild.Id,
                        Prefix = "."
                    };

                    await databaseContext.AddAsync(guildConfig);
                    await databaseContext.SaveChangesAsync();
                    
                    return ".";
                });
            }

            var argPos = 0;
            if (!message.HasStringPrefix(prefix, ref argPos))
                return;

            commandContext.ArgPos = argPos;

            await _commands.ExecuteAsync(commandContext, argPos, _services);
        }
    }
}