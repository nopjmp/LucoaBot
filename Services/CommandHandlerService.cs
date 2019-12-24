using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.Rest;
using Discord.WebSocket;
using LucoaBot.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly DatabaseContext _context;
        private readonly IServiceProvider _services;

        public CommandHandlerService(IServiceProvider services, DiscordSocketClient client, CommandService commands,
            DatabaseContext context, IMemoryCache cache)
        {
            _services = services;
            _client = client;
            _commands = commands;
            _context = context;
            _cache = cache;
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
            
            if (cmdContext.Guild != null)
            {
                // TODO: proper logging and reporting
                var guildUser = await cmdContext.Guild.GetCurrentUserAsync();
                if (!guildUser.GuildPermissions.Has(GuildPermission.SendMessages))
                    return;

                ChannelPermissions perms;
                if (cmdContext.Channel is IGuildChannel guildChannel)
                    perms = guildUser.GetPermissions(guildChannel);
                else
                    perms = ChannelPermissions.All(cmdContext.Channel);

                if (!perms.Has(ChannelPermission.SendMessages))
                    return;
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
                                var customCommand = await _context.CustomCommands.AsNoTracking()
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
            else // this is a successful command
            {
                CommandCount.WithLabels("success").Inc();
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

            var context = new CustomCommandContext(_client, message);

            if (!context.IsPrivate)
            {
                // TODO: move this into a caching layer
                var config = await _cache.GetOrCreateAsync("guildconfig:" + context.Guild.Id, async entry =>
                {
                    var guildConfig = await _context.GuildConfigs.AsNoTracking()
                        .Where(e => e.GuildId == context.Guild.Id)
                        .FirstOrDefaultAsync();

                    if (guildConfig == null)
                    {
                        guildConfig = new GuildConfig
                        {
                            GuildId = context.Guild.Id,
                            Prefix = "."
                        };

                        _context.Add(guildConfig);
                        _context.SaveChangesAsync().SafeFireAndForget(false);
                    }

                    return guildConfig;
                });

                prefix = config.Prefix;
            }

            var argPos = 0;
            if (!message.HasStringPrefix(prefix, ref argPos))
                return;

            context.ArgPos = argPos;

            await _commands.ExecuteAsync(context, argPos, _services);
        }
    }
}