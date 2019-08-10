using Discord;
using Discord.Commands;
using Discord.WebSocket;
using dotenv.net;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace LucoaBot
{
    internal class Bot
    {
        private ILogger logger = null;

        private readonly JsonSerializerSettings jss = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore
        };

        internal Bot()
        {
            DotEnv.Config(false);
            Log.Logger = new LoggerConfiguration()
              .Enrich.FromLogContext()
#if !DEBUG
              .Filter.ByExcluding(e =>
                {
                    bool result = false;

                    result |= Matching.FromSource("Microsoft.EntityFrameworkCore").Invoke(e)
                      && e.Level < LogEventLevel.Warning;

                    return result;
                })
#endif
              .WriteTo.Console()
              .CreateLogger();
        }

        internal async Task MainAsync()
        {
            using var services = BuildServiceProvider();

            logger = services.GetRequiredService<ILogger<Bot>>();
            logger.LogTrace("Loading configuration...");

            var client = services.GetRequiredService<DiscordSocketClient>();
            client.Log += LogAsync;
            services.GetRequiredService<CommandService>().Log += LogAsync;

            await client.LoginAsync(TokenType.Bot,
                Environment.GetEnvironmentVariable("DISCORD_TOKEN"));
            await client.StartAsync();

            await services.GetRequiredService<CommandHandlerService>().InitializeAsync();

            // warm database pool and check migrations
            if (services.GetRequiredService<DatabaseContext>().Database.GetPendingMigrations().Count() > 0)
            {
                throw new ApplicationException("You need to run the migrations...");
            }

            // TODO: add a condition for a service to use to kill the bot
            await Task.Delay(-1);
        }

        public ServiceProvider BuildServiceProvider() => new ServiceCollection()
            .AddMemoryCache()
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder.ClearProviders();
                loggingBuilder.SetMinimumLevel(LogLevel.Information);
                loggingBuilder.AddSerilog(dispose: true);

            })
            .AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Info,
                MessageCacheSize = 500,
            }))
            .AddSingleton(_ => new CommandService(new CommandServiceConfig
            {
                LogLevel = LogSeverity.Info,
                CaseSensitiveCommands = false,
                DefaultRunMode = RunMode.Async,
            }))
            .AddSingleton<CommandHandlerService>()
            .AddSingleton<HttpClient>()
            .AddDbContextPool<DatabaseContext>(options => options.UseNpgsql(Environment.GetEnvironmentVariable("DATASOURCE")))
            .BuildServiceProvider();

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
