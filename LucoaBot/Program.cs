using System.IO;
using System.Net.Http;
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
    internal static class Program
    {
        private const string Prefix = "LUCOA_";

        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureHostConfiguration(configHost =>
                {
                    configHost.SetBasePath(Directory.GetCurrentDirectory());
                    configHost.AddEnvironmentVariables(Prefix);
                    configHost.AddCommandLine(args);
                })
                .ConfigureAppConfiguration((hostContext, configApp) =>
                {
                    configApp.SetBasePath(Directory.GetCurrentDirectory());
                    configApp.AddEnvironmentVariables(Prefix);
                    configApp.AddJsonFile("appsettings.json", true);
                    configApp.AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", true);
                    configApp.AddCommandLine(args);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddMemoryCache();
                    services.AddLogging();
                    services.AddHttpClient();
                    services.AddHttpClient("noredirect")
                        .ConfigurePrimaryHttpMessageHandler(() =>
                            new HttpClientHandler
                            {
                                AllowAutoRedirect = false
                            });

                    services.AddSingleton<IMetricServer>(_ => new MetricServer(9091));

                    services.AddHostedService<ApplicationLifetimeHostedService>();

                    services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
                    {
                        LogLevel = LogSeverity.Info,
                        MessageCacheSize = 500
                    }));
                    services.AddSingleton(_ => new CommandService(new CommandServiceConfig
                    {
                        LogLevel = LogSeverity.Info,
                        CaseSensitiveCommands = false,
                        DefaultRunMode = RunMode.Async
                    }));
                    services.AddSingleton<CommandHandlerService>();
                    
                    services.AddDbContextPool<DatabaseContext>(options => options.UseNpgsql(
                        hostContext.Configuration.GetConnectionString("DefaultConnection"),
                        optionsBuilder => optionsBuilder.EnableRetryOnFailure(10)));
                    
                    services.AddSingleton<BusQueue>();
                    
                    services.AddSingleton<LogListener>();
                    services.AddSingleton<StarboardListener>();
                    services.AddSingleton<TemperatureListener>();
                    services.AddSingleton<QrCodeListener>();
                })
                .ConfigureLogging((hostContext, configLogging) =>
                {
                    configLogging.AddSerilog(new LoggerConfiguration()
                        .ReadFrom.Configuration(hostContext.Configuration)
                        .CreateLogger());
                    configLogging.AddDebug();
                })
                .UseConsoleLifetime(opts => opts.SuppressStatusMessages = true)
                .Build();

            await host.RunAsync();
        }
    }
}