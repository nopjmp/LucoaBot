using System;
using System.IO;
using System.Threading.Tasks;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using LucoaBot.Listeners;
using LucoaBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Prometheus;
using Serilog;

namespace LucoaBot
{
    class Program
    {
        private static readonly string PREFIX = "LUCOA_";
        public static async Task Main(string[] args)
        {
            IHost host = new HostBuilder()
                .ConfigureHostConfiguration(configHost =>
                {
                    configHost.SetBasePath(Directory.GetCurrentDirectory());
                    configHost.AddEnvironmentVariables(prefix: PREFIX);
                    configHost.AddCommandLine(args);
                })
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.SetBasePath(Directory.GetCurrentDirectory());
                    configApp.AddEnvironmentVariables(prefix: PREFIX);
                    configApp.AddJsonFile($"appsettings.json", true);
                    configApp.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", true);
                    configApp.AddCommandLine(args);

                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddMemoryCache();
                    services.AddLogging();

                    services.AddSingleton<IMetricServer>((_) => new MetricServer("localhost", 9091));

                    services.AddHostedService<ApplicationLifetimeHostedService>();

                    services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
                    {
                        LogLevel = LogSeverity.Info,
                        MessageCacheSize = 500,
                    }));
                    services.AddSingleton(_ => new CommandService(new CommandServiceConfig
                    {
                        LogLevel = LogSeverity.Info,
                        CaseSensitiveCommands = false,
                        DefaultRunMode = RunMode.Async,
                    }));
                    services.AddSingleton<CommandHandlerService>();
                    services.AddDbContextPool<DatabaseContext>(options => options.UseNpgsql(
                        hostContext.Configuration.GetConnectionString("DefaultConnection"),
                        options => options.EnableRetryOnFailure(10)));
                    services.AddSingleton<StarboardListener>();
                    services.AddSingleton<TemperatureListener>();
                })
                .ConfigureLogging((hostContext, configLogging) =>
                {
                    configLogging.AddSerilog(new LoggerConfiguration()
                        .ReadFrom.Configuration(hostContext.Configuration)
                        .CreateLogger());
                    configLogging.AddDebug();
                })
                .Build();

            await host.RunAsync();
        }
    }
}
