using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Reflection;

namespace SolarisBot.Discord.Services
{
    [AutoLoadService]
    internal sealed class InteractionHandlerService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _intService;
        private readonly BotConfig _config;
        private readonly ILogger<InteractionHandlerService> _logger;
        private readonly IServiceProvider _services;
        private readonly StatisticsService _stats;

        public InteractionHandlerService(DiscordSocketClient client, InteractionService interactions, BotConfig config, ILogger<InteractionHandlerService> logger, IServiceProvider services, StatisticsService stats)
        {
            _client = client;
            _intService = interactions;
            _config = config;
            _services = services;
            _logger = logger;
            _stats = stats;

            _intService.Log += logMessage => logMessage.Log(_logger);
        }

        /// <summary>
        /// Loads in all interaction modules and registers them
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client.InteractionCreated += HandleInteractionCreated;
            _intService.InteractionExecuted += HandleInteractionExecuted;

            foreach(var type in GetType().Assembly.GetTypes())
            {
                if (!type.IsSubclassOf(typeof(SolarisInteractionModuleBase))) continue;
                var attribute = type.GetCustomAttribute<ModuleAttribute>();
                var moduleNamesText = attribute is null ? "NONE" : string.Join(" + ", attribute.ModuleNames);
                if (attribute?.IsDisabled(_config.DisabledModules) ?? false)
                {
                    _logger.LogDebug("Skipping adding InteractionModule {intModule} from disabled module {module}", type.FullName, moduleNamesText);
                    continue;
                }
                _logger.LogDebug("Adding InteractionModule {intModule} from module {module}", type.FullName, moduleNamesText);
                await _intService.AddModuleAsync(type, _services);
            }

#if DEBUG
            _client.Ready += RegisterInteractionsToMainAsync;
#else
            _client.Ready += RegisterInteractionsGloballyAsync;
#endif
        }

#pragma warning disable IDE0051 // Remove unused private members
        /// <summary>
        /// Registers all interactions to main guild
        /// </summary>
        private async Task RegisterInteractionsToMainAsync()
        {
            _logger.LogInformation("Ready in DEBUG");
            var guild = _client.GetGuild(_config.MainGuild);
            if (guild != null)
            {
                _logger.LogInformation("Registering interactions to guild {guild}", guild.Log());
                await _intService.RegisterCommandsToGuildAsync(_config.MainGuild);
            }
        }

        /// <summary>
        /// Registers all interactions globally
        /// </summary>
        private async Task RegisterInteractionsGloballyAsync()
        {
            _logger.LogInformation("Ready in RELEASE");
            var guild = _client.GetGuild(_config.MainGuild);
            if (guild is not null)
            {
                _logger.LogInformation("Unregistering interactions to guild {guild}", guild.Log());
                await guild.DeleteApplicationCommandsAsync();
            }
            _logger.LogInformation("Registering interactions globally");
            await _intService.RegisterCommandsGloballyAsync();
        }
#pragma warning restore IDE0051 // Remove unused private members

        /// <summary>
        /// Stops the Interaction handler
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.InteractionCreated -= HandleInteractionCreated;
            _intService.InteractionExecuted -= HandleInteractionExecuted;
            _client.Ready -= RegisterInteractionsToMainAsync;

            _intService.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles an interaction being created and executes it
        /// </summary>
        private async Task HandleInteractionCreated(SocketInteraction interaction)
        {
            var cmdName = "N/A";
            if (interaction is SocketCommandBase scb)
                cmdName = scb.CommandName;

            var context = new SocketInteractionContext(_client, interaction);
            _logger.LogDebug("Executing interaction \"{interactionName}\"({interactionId}) for user {user} in channel {channel} of guild {guild}", cmdName, interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
            var result = await _intService.ExecuteCommandAsync(context, _services);

            //this will happen when the command cant be found
            if (!result.IsSuccess)
            {
                _logger.LogError("Failed executing interaction \"{interactionName}\"({interactionId}) for user {user} in channel {channel} of guild {guild} => {error}: {reason}", cmdName, interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A", result.Error.ToString()!, result.ErrorReason);
                await context.Interaction.ReplyErrorAsync($"{result.Error!}: {result.ErrorReason}");
            }
        }

        /// <summary>
        /// Handles the result of an interaction
        /// </summary>
        private async Task HandleInteractionExecuted(ICommandInfo cmdInfo, IInteractionContext context, IResult result)
        {
            if (result.IsSuccess)
            {
                _logger.LogDebug("Executed interaction \"{interactionModule}\"(Module {module}, Id {interactionId}) for user {user} in channel {channel} of guild {guild}", cmdInfo?.Name ?? "N/A", cmdInfo?.Module.Name ?? "N/A", context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
                _stats.IncreaseCommandsExecuted();
                return;
            }

            if (result is ExecuteResult exeResult)
            {
                var exception = exeResult.Exception;
                while(exception.InnerException is not null)
                    exception = exception.InnerException;

                _logger.LogError(exeResult.Exception, "Failed to execute interaction \"{interactionModule}\"(Module {module}, Id {interactionId}) for user {user} in channel {channel} of guild {guild}", cmdInfo?.Name ?? "N/A", cmdInfo?.Module.Name ?? "N/A", context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
                await context.Interaction.ReplyErrorAsync($"{exception.GetType().Name}: {exception.Message}");
            }
            else
            {
                _logger.LogError("Failed to execute interaction \"{interactionModule}\"(Module {module}, Id {interactionId}) for user {user} in channel {channel} of guild {guild} => {error}: {reason}", cmdInfo?.Name ?? "N/A", cmdInfo?.Module.Name ?? "N/A", context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A", result.Error.ToString()!, result.ErrorReason);
                await context.Interaction.ReplyErrorAsync($"{result.Error!}: {result.ErrorReason}");
            }
            _stats.IncreaseCommandsFailed();
        }
    }
}
