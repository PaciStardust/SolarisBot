using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Discord.Services
{
    internal sealed class InteractionHandlerService : IHostedService //todo: [FEATURE] How do other bots do this?
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

#if DEBUG
            _client.Ready += RegisterCommandsToMainAsync;
#else
            _client.Ready += RegisterCommandsGloballyAsync;
#endif
        }

#pragma warning disable IDE0051 // Remove unused private members
        private async Task RegisterCommandsToMainAsync() //todo: [FEATURE] Log this
        {
            if (_client.Guilds.Any(x => x.Id == _config.MainGuild))
                await _intService.RegisterCommandsToGuildAsync(_config.MainGuild).ConfigureAwait(false);
        }

        private async Task RegisterCommandsGloballyAsync()
        {
            var guild = _client.GetGuild(_config.MainGuild);
            if (guild is not null) //todo: [TEST] Does this maybe work on Globals???
                await guild.DeleteApplicationCommandsAsync().ConfigureAwait(false);
            await _intService.RegisterCommandsGloballyAsync().ConfigureAwait(false);
        }
#pragma warning restore IDE0051 // Remove unused private members

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _intService.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles an interaction
        /// </summary>
        private async Task HandleInteraction(SocketInteraction interaction) //todo: [REFACTOR] Can this be used to catch exceptions better?
        {
            var cmdName = "N/A";
            if (interaction is SocketCommandBase scb)
                cmdName = scb.CommandName;

            var context = new SocketInteractionContext(_client, interaction);
            _logger.LogDebug("Executing interaction \"{interactionName}\"({interactionId}) for user {user} in channel {channel} of guild {guild}", cmdName, interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
            var result = await _intService.ExecuteCommandAsync(context, _services);

            if (!result.IsSuccess)
            {
                _logger.LogDebug("Failed executing interaction \"{interactionName}\"({interactionId}) for user {user} in channel {channel} of guild {guild}, reason {reason}", cmdName, interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A", result.ErrorReason);
                var responseEmbed = DiscordUtils.EmbedError("Interaction Error", result.ErrorReason); //todo: [REFACTOR] remove title, move?
                await context.Interaction.RespondAsync(embed: responseEmbed, ephemeral: true);
            }
            _logger.LogInformation("Executed interaction \"{interactionName}\"({interactionId}) for user {user} in channel {channel} of guild {guild}", cmdName, interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
        }
    }
}
