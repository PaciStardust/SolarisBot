using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SolarisBot.ConfigFiles;
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
            BotConfig botConfig;
            var assembly = Assembly.GetExecutingAssembly();
            try
            {
                botConfig = GetOrCreateBotConfig();
                ConfigFileProvider.LoadConfigFiles(assembly);
            } 
            catch (Exception ex)
            {
                Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
                throw;
            }

            var configuration = CreateConfiguration();

            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();
            logger.Information("SolarisBot by PaciStardust is starting");

            logger.Information("Initializing hosting, building host");
            var host = CreateHost(configuration, botConfig, logger, assembly);

            logger.Information("Build complete, starting host");

            await host.Services.GetRequiredService<DatabaseService>().ReadyAsync(); //todo: [REFACTOR] Move this?

            await host.RunAsync();
        }

        #region Setup
        private static IConfiguration CreateConfiguration()
            => new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(Path.Combine(Utils.PathConfigDirectory, "appsettings.json"), true, true)
                .AddJsonFile(Path.Combine(Utils.PathConfigDirectory, $"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Unknown"}.json"), true, true)
                .AddEnvironmentVariables()
                .Build();

        private static IHost CreateHost(IConfiguration configuration, BotConfig botConfig, ILogger logger, Assembly assembly) //todo: [FEATURE] Backups of database, counting?
            => Host.CreateDefaultBuilder() 
                .ConfigureAppConfiguration(config => config.AddConfiguration(configuration))
                .ConfigureServices(services =>
                {
                    services.AddSingleton<DatabaseService>(); //todo: [REFACTOR] make this automatic at some point

                    services.AddHttpClient();

                    services.AddSingleton(botConfig);
                    services.AddSingleton(new DiscordSocketClient(new()
                    {
                        GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent | GatewayIntents.GuildMembers,
                        UseInteractionSnowflakeDate = false,
                        DefaultRetryMode = RetryMode.RetryRatelimit
                    }));

                    //Fix for constructor of interaction service being broken (Provided by Discord.NET discord)
                    services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));

                    foreach (var service in assembly.GetTypes())
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

        /// <summary>
        /// Loads a config or generates a new one if needed, updates it and saves it
        /// </summary>
        /// <returns>Updated BotConfig</returns>
        private static BotConfig GetOrCreateBotConfig()
        {
            Console.WriteLine($"Loading config from {Utils.PathConfigFile}");

            var botConfig = BotConfig.FromFile(Utils.PathConfigFile) ?? new();

            if (string.IsNullOrWhiteSpace(botConfig.Token))
            {
                Console.Write("Token > ");
                botConfig.Token = Console.ReadLine() ?? string.Empty;
            }
            if (botConfig.MainGuild == 0)
            {
                Console.Write("Main Guild > ");
                botConfig.MainGuild = ulong.Parse(Console.ReadLine() ?? string.Empty);
            }

            Console.WriteLine("Updating and saving config");
            botConfig.Update();
            botConfig.SaveAt(Utils.PathConfigFile);

            return botConfig;
        }
        #endregion
    }
}
