using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SolarisBot.Database;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Services;
using System.Reflection;

namespace SolarisBot
{
    internal static class Program
    {
        static async Task Main(string[] args)
        {
            var configuration = CreateConfiguration();

            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            logger.Information("SolarisBot by PaciStardust is starting");

            logger.Information("Loading BotConfig from {cfgPath}", Utils.PathConfigFile);
            var botConfig = GetConfig();
            if (!botConfig.SaveAt(Utils.PathConfigFile))
                logger.Warning("Failed to save BotConfig");
            logger.Information("Successfully loaded BotConfig");

            logger.Information("Initializing hosting, building host");
            var host = CreateHost(configuration, botConfig, logger);

            logger.Information("Build complete, starting host");
            await host.RunAsync();
        }

        #region Setup
        private static IConfiguration CreateConfiguration()
            => new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true, true)
                .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"}.json", true, true)
                .AddEnvironmentVariables()
                .Build();

        private static IHost CreateHost(IConfiguration configuration, BotConfig botConfig, ILogger logger)
            => Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration(config => config.AddConfiguration(configuration))
                .ConfigureServices(services =>
                {
                    services.AddDbContext<DatabaseContext>(options => options.UseSqlite
                    (
                        $"Data Source={Utils.PathDatabaseFile};Pooling=false")
                    );

                    services.AddHttpClient();

                    services.AddSingleton(botConfig);
                    services.AddSingleton(new DiscordSocketClient(new()
                    {
                        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
                        UseInteractionSnowflakeDate = false
                    }));
                    services.AddSingleton<InteractionService>();

                    foreach (var service in Assembly.GetExecutingAssembly().GetTypes())
                    {
                        var autoLoadAttribute = service.GetCustomAttribute<AutoLoadServiceAttribute>();
                        if (autoLoadAttribute is null)
                            continue;

                        bool isHosted = typeof(IHostedService).IsAssignableFrom(service);

                        var attribute = service.GetCustomAttribute<ModuleAttribute>();
                        if (attribute?.IsDisabled(botConfig.DisabledModules) ?? false)
                        {
                            logger.Debug("Skipping adding {serviceType} {service} from disabled module {module}", isHosted ? "HostedService" : "Service", service.FullName, attribute.ModuleName);
                            continue;
                        }
                        logger.Debug("Adding {serviceType} {service} from module {module}", isHosted ? "HostedService" : "Service", service.FullName, attribute?.ModuleName ?? "NONE");

                        if (isHosted)
                            services.AddSingleton(typeof(IHostedService), service);
                        else
                        {
                            switch (autoLoadAttribute.Lifetime)
                            {
                                case Lifetime.Transient: services.AddTransient(service); break;
                                case Lifetime.Scoped: services.AddScoped(service); break;
                                default: services.AddSingleton(service); break;
                            }
                        }
                    }
                    services.AddHostedService<DiscordClientService>();
                })
                .UseSerilog(logger)
                .Build();

        private static BotConfig GetConfig()
        {
            var botConfig = BotConfig.FromFile(Utils.PathConfigFile);
            if (botConfig is not null)
                return botConfig;

            botConfig = new();
            Console.Write("Token > ");
            botConfig.Token = Console.ReadLine() ?? string.Empty;
            Console.Write("Main Guild > ");
            botConfig.MainGuild = ulong.Parse(Console.ReadLine() ?? string.Empty);

            return botConfig;
        }
        #endregion
    }
}
