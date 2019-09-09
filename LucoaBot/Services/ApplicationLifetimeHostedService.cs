using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LucoaBot.Listeners;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LucoaBot.Services
{
    public class ApplicationLifetimeHostedService : IHostedService
    {
        ILogger<ApplicationLifetimeHostedService> logger;
        IConfiguration configuration;

        DiscordSocketClient discordSocketClient;
        CommandService commandService;
        CommandHandlerService commandHandlerService;

        // TODO: make this an array of listeners...
        StarboardListener starboardListener;
        TemperatureListener temperatureListener;
        DatabaseContext databaseContext;

        private CancellationTokenSource userCountTokenSource = null;

        public ApplicationLifetimeHostedService(
            IConfiguration configuration,
            ILogger<ApplicationLifetimeHostedService> logger,
            DiscordSocketClient discordSocketClient,
            CommandService commandService,
            CommandHandlerService commandHandlerService,
            StarboardListener starboardListener,
            TemperatureListener temperatureListener,
            DatabaseContext databaseContext)
        {
            this.configuration = configuration;
            this.logger = logger;
            this.discordSocketClient = discordSocketClient;
            this.commandService = commandService;
            this.commandHandlerService = commandHandlerService;
            this.starboardListener = starboardListener;
            this.temperatureListener = temperatureListener;
            this.databaseContext = databaseContext;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            logger.LogInformation("Starting up application.");

            // warm database pool and check migrations
            if (databaseContext.Database.GetPendingMigrations().Count() > 0)
            {
                throw new ApplicationException("You need to run the migrations...");
            }

            discordSocketClient.Connected += () =>
            {
                // Setup Cancellation for when we disconnect.
                userCountTokenSource = new CancellationTokenSource();
                var cancellationToken = userCountTokenSource.Token;

                var userCountTask = Task.Run(async () =>
                {
                    var lastCount = -1;
                    while (true)
                    {
                        var count = discordSocketClient.Guilds.Aggregate(0, (a, g) => a + g.MemberCount);
                        if (count != lastCount)
                        {
                            lastCount = count;
                            if (count > 0)
                                await discordSocketClient.SetActivityAsync(new Game($"{count} users", ActivityType.Watching));
                        }
                        await Task.Delay(10000);
                    }
                }, cancellationToken);

                return Task.CompletedTask;
            };

            discordSocketClient.Disconnected += (_) =>
            {
                if (!userCountTokenSource.IsCancellationRequested)
                {
                    userCountTokenSource.Cancel();
                }

                return Task.CompletedTask;
            };

            discordSocketClient.Log += LogAsync;
            commandService.Log += LogAsync;

            await discordSocketClient.LoginAsync(TokenType.Bot, configuration["Token"]);
            await discordSocketClient.StartAsync();

            await commandHandlerService.InitializeAsync();

            starboardListener.Initialize();
            temperatureListener.Initialize();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await discordSocketClient.SetStatusAsync(UserStatus.Invisible);
            await discordSocketClient.StopAsync();
        }

        private Task LogAsync(LogMessage msg)
        {
            LogLevel logLevel = LogLevel.None;
            switch (msg.Severity)
            {
                case LogSeverity.Critical:
                    logLevel = LogLevel.Critical;
                    break;
                case LogSeverity.Error:
                    logLevel = LogLevel.Error;
                    break;
                case LogSeverity.Warning:
                    logLevel = LogLevel.Warning;
                    break;
                case LogSeverity.Info:
                    logLevel = LogLevel.Information;
                    break;
                case LogSeverity.Verbose:
                    logLevel = LogLevel.Trace;
                    break;
                case LogSeverity.Debug:
                    logLevel = LogLevel.Debug;
                    break;
            }
            // TODO: source?
            logger.Log(logLevel, msg.Exception, msg.Message);

            return Task.CompletedTask;
        }
    }
}
