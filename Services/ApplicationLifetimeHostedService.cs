using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LucoaBot.Listeners;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;

namespace LucoaBot.Services
{
    public class ApplicationLifetimeHostedService : IHostedService
    {
        private readonly ILogger<ApplicationLifetimeHostedService> _logger;
        private readonly IConfiguration _configuration;

        private readonly IMetricServer _metricServer;

        private readonly DiscordSocketClient _discordClient;
        private readonly CommandService _commandService;
        private readonly CommandHandlerService _commandHandlerService;

        // TODO: make this an array of listeners...
        private readonly LogListener _logListener;
        private readonly StarboardListener _starboardListener;
        private readonly TemperatureListener _temperatureListener;
        private readonly DatabaseContext _databaseContext;

        private CancellationTokenSource _userCountTokenSource;

        private static readonly Gauge UserCounterGauge =
            Metrics.CreateGauge("discord_users", "currently observed discord users");

        private static readonly Gauge LatencyGauge =
            Metrics.CreateGauge("discord_latency", "currently observed discord users");

        public ApplicationLifetimeHostedService(
            IConfiguration configuration,
            ILogger<ApplicationLifetimeHostedService> logger,
            IMetricServer metricServer,
            DiscordSocketClient discordClient,
            CommandService commandService,
            CommandHandlerService commandHandlerService,
            LogListener logListener,
            StarboardListener starboardListener,
            TemperatureListener temperatureListener,
            DatabaseContext databaseContext)
        {
            _configuration = configuration;
            _logger = logger;
            _metricServer = metricServer;
            _discordClient = discordClient;
            _commandService = commandService;
            _commandHandlerService = commandHandlerService;
            _logListener = logListener;
            _starboardListener = starboardListener;
            _temperatureListener = temperatureListener;
            _databaseContext = databaseContext;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting up application.");

            // warm database pool and check migrations
            if (_databaseContext.Database.GetPendingMigrations().Any())
            {
                throw new ApplicationException("You need to run the migrations...");
            }

            _metricServer.Start();

            _discordClient.Connected += () =>
            {
                // Setup Cancellation for when we disconnect.
                _userCountTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                var userCountToken = _userCountTokenSource.Token;

                Task.Run(async () =>
                {
                    var lastCount = -1;
                    while (true)
                    {
                        var count = _discordClient.Guilds.Aggregate(0, (a, g) => a + g.MemberCount);
                        if (count != lastCount)
                        {
                            UserCounterGauge.Set(count);
                            lastCount = count;
                            if (count > 0)
                                await _discordClient.SetActivityAsync(new Game($"{count} users",
                                    ActivityType.Watching));
                        }

                        await Task.Delay(10000, userCountToken);
                    }
                }, userCountToken);

                return Task.CompletedTask;
            };

            _discordClient.Disconnected += _ =>
            {
                if (_userCountTokenSource != null)
                {
                    _userCountTokenSource.Cancel();
                    _userCountTokenSource.Dispose();
                    _userCountTokenSource = null;
                }

                return Task.CompletedTask;
            };

            _discordClient.LatencyUpdated += (_, val) =>
            {
                LatencyGauge.Set(val);
                return Task.CompletedTask;
            };

            _discordClient.Log += LogAsync;
            _commandService.Log += LogAsync;

            await _discordClient.LoginAsync(TokenType.Bot, _configuration["Token"]);
            await _discordClient.StartAsync();

            await _commandHandlerService.InitializeAsync();

            _logListener.Initialize();
            _starboardListener.Initialize();
            _temperatureListener.Initialize();
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_userCountTokenSource != null)
            {
                _userCountTokenSource.Cancel();
                _userCountTokenSource.Dispose();
                _userCountTokenSource = null;
            }

            if (_discordClient.ConnectionState == ConnectionState.Connected)
            {
                await _discordClient.SetStatusAsync(UserStatus.Invisible);
                await _discordClient.StopAsync();
            }

            await _metricServer.StopAsync();
        }

        private Task LogAsync(LogMessage msg)
        {
            var logLevel = msg.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Verbose => LogLevel.Trace,
                LogSeverity.Debug => LogLevel.Debug,
                _ => LogLevel.None
            };
            // Note: possibly should implement source
            _logger.Log(logLevel, msg.Exception, msg.Message);

            return Task.CompletedTask;
        }
    }
}