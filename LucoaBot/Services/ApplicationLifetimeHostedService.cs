using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DSharpPlus;
using DSharpPlus.Entities;
using LucoaBot.Listeners;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LucoaBot.Services
{
    public class ApplicationLifetimeHostedService : IHostedService
    {
        private readonly CommandHandlerService _commandHandlerService;
        private readonly IConfiguration _configuration;
        private readonly DatabaseContext _databaseContext;

        private readonly DiscordClient _discordClient;

        private readonly ILogger<DiscordClient> _discordLogger;
        private readonly ILogger<ApplicationLifetimeHostedService> _logger;

        // TODO: make this an array of listeners...
        private readonly LogListener _logListener;
        private readonly QrCodeListener _qrCodeListener;

        private readonly StarboardListener _starboardListener;
        private readonly TemperatureListener _temperatureListener;

        public ApplicationLifetimeHostedService(
            IConfiguration configuration,
            ILogger<ApplicationLifetimeHostedService> logger,
            ILogger<DiscordClient> discordLogger,
            DiscordClient discordClient,
            CommandHandlerService commandHandlerService,
            LogListener logListener,
            StarboardListener starboardListener,
            TemperatureListener temperatureListener,
            DatabaseContext databaseContext, QrCodeListener qrCodeListener)
        {
            _configuration = configuration;
            _logger = logger;
            _discordLogger = discordLogger;
            _discordClient = discordClient;
            _commandHandlerService = commandHandlerService;
            _logListener = logListener;
            _starboardListener = starboardListener;
            _temperatureListener = temperatureListener;
            _databaseContext = databaseContext;
            _qrCodeListener = qrCodeListener;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting up application.");

            // warm database pool and check migrations
            if (_databaseContext.Database.GetPendingMigrations().Any())
                await _databaseContext.Database.MigrateAsync();

            //throw new ApplicationException("You need to run the migrations...");

            //_metricServer.Start();

            // _discordClient.Logger.LogMessageReceived += (sender, eventArgs) =>
            // {
            //     _discordLogger.Log(eventArgs.Level.AsLoggingLevel(), eventArgs.Exception, eventArgs.Message);
            // };

            await _discordClient.ConnectAsync();

            _logListener.Initialize();
            _starboardListener.Initialize();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await _discordClient.DisconnectAsync();
        }
    }
}