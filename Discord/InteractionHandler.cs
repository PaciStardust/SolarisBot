using Discord.Interactions;
using Discord.WebSocket;

namespace SolarisBot.Discord
{
    public class InteractionHandler
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _intService;
        private readonly IServiceProvider _services;

        public InteractionHandler(DiscordSocketClient client, InteractionService intService, IServiceProvider services)
        {
            _client = client;
            _intService = intService;
            _services = services;

            _client.InteractionCreated += HandleInteraction;
        }

        /// <summary>
        /// Loads all interactions
        /// </summary>
        public async Task InitializeAsync()
        {
            await _intService.AddModulesAsync(GetType().Assembly, _services);
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
        private static async Task HandleInteractionErrors(IResult result, SocketInteractionContext context)
        {
            Logger.Debug($"Executed command {context.Interaction.Id} for user {context.User.Username}({context.User.Id})");

            if (result.IsSuccess) return;
            await context.Interaction.RespondAsync(embed: Embeds.Error("Interaction Error", result.ErrorReason));
        }
    }
}
