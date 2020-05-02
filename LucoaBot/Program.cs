using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using DSharpPlus;
using EFCoreSecondLevelCacheInterceptor;
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
                .ConfigureAppConfiguration((hostContext, config) =>
                {
                    config.SetBasePath(Directory.GetCurrentDirectory());
                    config.AddJsonFile("appsettings.json", true)
                        .AddJsonFile($"appsettings.{hostContext.HostingEnvironment.EnvironmentName}.json", true);

                    config.AddEnvironmentVariables(Prefix)
                        .AddCommandLine(args);
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

                    services.AddEFSecondLevelCache(options => options.UseMemoryCacheProvider());

                    services.AddHostedService<ApplicationLifetimeHostedService>();

                    services.AddSingleton(new DiscordClient(new DiscordConfiguration()
                    {
                        TokenType = TokenType.Bot,
                        Token = hostContext.Configuration["Token"],
                        AutoReconnect = true,
#if DEBUG
                        LogLevel = DSharpPlus.LogLevel.Debug,
#endif
                        UseInternalLogHandler = false
                    }));

                    services.AddSingleton<CommandHandlerService>();

                    services.AddDbContext<DatabaseContext>(options =>
                    {
                        options.UseNpgsql(
                            hostContext.Configuration.GetConnectionString("DefaultConnection"),
                            optionsBuilder => optionsBuilder.EnableRetryOnFailure(10));
                    });

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