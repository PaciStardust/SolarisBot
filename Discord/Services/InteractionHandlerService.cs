using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Discord.Services
{
    internal sealed class InteractionHandlerService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _intService;
        private readonly BotConfig _config;
        private readonly ILogger<InteractionHandlerService> _logger;
        private readonly IServiceProvider _services;

        public InteractionHandlerService(DiscordSocketClient client, InteractionService interactions, BotConfig config, ILogger<InteractionHandlerService> logger, IServiceProvider services)
        {
            _client = client;
            _intService = interactions;
            _config = config;
            _services = services;
            _logger = logger;

            _intService.Log += logMessage => logMessage.Log(_logger);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client.InteractionCreated += HandleInteraction;
            await _intService.AddModulesAsync(GetType().Assembly, _services);

            if (_config.GlobalLoad)
            {
                await _intService.RegisterCommandsToGuildAsync(_config.MainGuild); //this will clear them locally
                _client.Ready += () => _intService.RegisterCommandsGloballyAsync();
            }
            else
            {
                _client.Ready += () => _intService.RegisterCommandsToGuildAsync(_config.MainGuild);
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _intService.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles an interaction
        /// </summary>
        private async Task HandleInteraction(SocketInteraction interaction) //todo: [FEATURE] Can this be used to catch exceptions better?
        {
            var context = new SocketInteractionContext(_client, interaction);
            _logger.LogDebug("Executing command {interactionId} for user {user}", context.Interaction.Id, context.User.Log());
            var result = await _intService.ExecuteCommandAsync(context, _services);

            if (!result.IsSuccess)
            {
                _logger.LogError("Failed to execute command {interactionId} for user {user} ({resultError})", context.Interaction.Id, context.User.Log(), result.Error);
                var responseEmbed = DiscordUtils.EmbedError("Interaction Error", result.ErrorReason);
                await context.Interaction.RespondAsync(embed: responseEmbed, ephemeral: true);
            }
            _logger.LogDebug("Executed command {interactionId} for user {user}", context.Interaction.Id, context.User.Log());
        }
    }
}
