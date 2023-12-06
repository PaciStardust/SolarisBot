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

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client.InteractionCreated += HandleInteractionCreated;
            _intService.InteractionExecuted += HandleInteractionExecuted;

            foreach(var type in GetType().Assembly.GetTypes())
            {
                if (!type.IsSubclassOf(typeof(SolarisInteractionModuleBase))) continue;
                var attribute = type.GetCustomAttribute<ModuleAttribute>();
                if (attribute?.IsDisabled(_config.DisabledModules) ?? false)
                {
                    _logger.LogDebug("Skipping adding InteractionModule {intModule} from disabled module {module}", type.FullName, attribute.ModuleName);
                    continue;
                }
                _logger.LogDebug("Adding InteractionModule {intModule} from module {module}", type.FullName, attribute?.ModuleName ?? "NONE");
                await _intService.AddModuleAsync(type, _services);
            }

#if DEBUG
            _client.Ready += RegisterInteractionsToMainAsync;
#else
            _client.Ready += RegisterCommandsGloballyAsync;
#endif
        }

#pragma warning disable IDE0051 // Remove unused private members
        private async Task RegisterInteractionsToMainAsync()
        {
            var guild = _client.GetGuild(_config.MainGuild);
            if (guild != null)
            {
                _logger.LogInformation("Registering interactions to guild {guild}", guild.Log());
                await _intService.RegisterCommandsToGuildAsync(_config.MainGuild);
            }
        }

        private async Task RegisterInteractionsGloballyAsync()
        {
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
                _logger.LogInformation("Executed interaction \"{interactionModule}\"(Module {module}, Id {interactionId}) for user {user} in channel {channel} of guild {guild}", cmdInfo?.Name ?? "N/A", cmdInfo?.Module.Name ?? "N/A", context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
                _stats.IncreaseCommandsExecuted();
                return;
            }

            if (result is ExecuteResult exeResult)
            {
                _logger.LogError(exeResult.Exception, "Failed to execute interaction \"{interactionModule}\"(Module {module}, Id {interactionId}) for user {user} in channel {channel} of guild {guild}", cmdInfo?.Name ?? "N/A", cmdInfo?.Module.Name ?? "N/A", context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
                await context.Interaction.ReplyErrorAsync($"{exeResult.Exception.GetType().Name}: {exeResult.Exception.Message}");
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
