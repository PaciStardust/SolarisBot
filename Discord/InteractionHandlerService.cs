using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Discord
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
            _client.Ready += () => _intService.RegisterCommandsToGuildAsync(_config.MainGuild); //todo: global loading
            await _intService.AddModulesAsync(GetType().Assembly, _services);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _intService.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles an interaction
        /// </summary>
        private async Task HandleInteraction(SocketInteraction interaction)
        {
            var context = new SocketInteractionContext(_client, interaction);
            var result = await _intService.ExecuteCommandAsync(context, _services);

            await HandleInteractionErrors(result, context);
        }

        /// <summary>
        /// Handles errors caused by interactions
        /// </summary>
        private async Task HandleInteractionErrors(IResult result, SocketInteractionContext context)
        {
            _logger.LogDebug("Executed command {interactionId} for user {userName}({userId})", context.Interaction.Id, context.User.Username, context.User.Id);
            if (result.IsSuccess) return;
            var responseEmbed = DiscordUtils.ErrorEmbed("Interaction Error", result.ErrorReason);
            await context.Interaction.RespondAsync(embed: responseEmbed, ephemeral: true);
        }
    }
}
