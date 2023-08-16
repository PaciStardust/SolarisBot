using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Discord.WebSocket;
using Discord;
using Discord.Interactions;
using SolarisBot.Discord;
using SolarisBot.Database;
using Microsoft.EntityFrameworkCore;

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
            logger.Information("PopupBot by PaciStardust is starting");

            logger.Information("Loading BotConfig from {cfgPath}", Utils.PathConfigFile);
            var botConfig = GetConfig();
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
                        .UseLazyLoadingProxies()
                    );
                    services.AddSingleton(botConfig);
                    services.AddSingleton(new DiscordSocketClient(new()
                    {
                        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
                        UseInteractionSnowflakeDate = false
                    }));
                    services.AddSingleton<InteractionService>();
                    services.AddHostedService<InteractionHandlerService>();
                    services.AddHostedService<DiscordClientService>();
                })
                .UseSerilog(logger)
                .Build();

        private static BotConfig GetConfig()
        {
            var botConfig = BotConfig.FromFile(Utils.PathConfigFile);
            if (botConfig != null)
                return botConfig;

            botConfig = new();
            Console.Write("Token > ");
            botConfig.Token = Console.ReadLine() ?? string.Empty;
            Console.Write("Main Guild > ");
            botConfig.MainGuild = ulong.Parse(Console.ReadLine() ?? string.Empty);

            var saved = botConfig.SaveAt(Utils.PathConfigFile);
            Console.WriteLine(saved ? "Successfully saved config" : "Unable to save config");
            return botConfig;
        }
        #endregion
    }
}
