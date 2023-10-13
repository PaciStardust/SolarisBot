using Discord;
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
            _client.InteractionCreated += HandleInteractionCreated;
            _intService.InteractionExecuted += HandleInteractionExecuted;
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
                await _intService.RegisterCommandsToGuildAsync(_config.MainGuild);
        }

        private async Task RegisterCommandsGloballyAsync()
        {
            var guild = _client.GetGuild(_config.MainGuild);
            if (guild is not null) //todo: [TEST] Does this maybe work on Globals???
                await guild.DeleteApplicationCommandsAsync();
            await _intService.RegisterCommandsGloballyAsync();
        }
#pragma warning restore IDE0051 // Remove unused private members

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _intService.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles an interaction being created and executes it
        /// </summary>
        private async Task HandleInteractionCreated(SocketInteraction interaction) //todo: [REFACTOR] Can this be used to catch exceptions better?
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
                var responseEmbed = DiscordUtils.EmbedError($"Error - {result.Error!}", result.ErrorReason); //todo: [REFACTOR] remove title, move?
                await context.Interaction.RespondAsync(embed: responseEmbed, ephemeral: true);
            }
        }

        /// <summary>
        /// Handles the result of an interaction
        /// </summary>
        private async Task HandleInteractionExecuted(ICommandInfo cmdInfo, IInteractionContext context, IResult result)
        {
            var paramInfo = string.Join(", ", cmdInfo.Parameters); //todo: [TEST] does this look decent?

            if (result.IsSuccess)
            {
                _logger.LogInformation("Executed interaction \"{interactionModule}\"(Module {module}, Params {parameters}, Id {interactionId}) for user {user} in channel {channel} of guild {guild}", cmdInfo.Name, cmdInfo.Module, paramInfo, context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
                return;
            }

            if (result is ExecuteResult exeResult)
            {
                _logger.LogError(exeResult.Exception, "Failed to execute interaction \"{interactionModule}\"(Module {module}, Params {parameters}, Id {interactionId}) for user {user} in channel {channel} of guild {guild}", cmdInfo.Name, cmdInfo.Module, paramInfo, context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
                var responseEmbed = DiscordUtils.EmbedError($"Error - {exeResult.Exception.GetType().Name}", exeResult.Exception.Message);
                await context.Interaction.RespondAsync(embed: responseEmbed, ephemeral: true);
            }
            else
            {
                _logger.LogError("Failed to execute interaction \"{interactionModule}\"(Module {module}, Params {parameters}, Id {interactionId}) for user {user} in channel {channel} of guild {guild} => {error}: {reason}", cmdInfo.Name, cmdInfo.Module, paramInfo, context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A", result.Error.ToString()!, result.ErrorReason);
                var responseEmbed = DiscordUtils.EmbedError($"Error - {result.Error!}", result.ErrorReason);
                await context.Interaction.RespondAsync(embed: responseEmbed, ephemeral: true);
            }
        }
    }
}
