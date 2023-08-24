using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Discord.Commands
{
    [Group("owner", "[OWNER ONLY] Configure Solaris"), RequireContext(ContextType.Guild), RequireOwner] //todo: [TESTING] Test if visible to others
    public sealed class OwnerCommands : SolarisInteractionModuleBase
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OwnerCommands> _logger;
        internal OwnerCommands(IServiceProvider services, ILogger<OwnerCommands> logger)
        {
            _services = services;
            _logger = logger;
        }
        protected override ILogger? GetLogger() => _logger;

        [SlashCommand("set-status", "Set the status of the bot")] //todo: [TESTING] Does command work?
        public async Task SetStatusAsync(string status)
        {
            var config = _services.GetRequiredService<BotConfig>();
            config.DefaultStatus = status;

            if(!config.SaveAt(Utils.PathConfigFile))
            {
                await RespondErrorEmbedAsync("Set Status Failed", "Unable to save new status in config file");
                return;
            }

            var client = _services.GetRequiredService<DiscordSocketClient>();
            try
            {
                await client.SetGameAsync(status);
                _logger.LogInformation("Discord client status has been set to {discordStatus}", status);
                await RespondEmbedAsync("Status Set", $"Status set to \"{status}\")");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set discord client status to {discordStatus}", status);
                await RespondErrorEmbedAsync(ex);
            }
        }
    }
}
