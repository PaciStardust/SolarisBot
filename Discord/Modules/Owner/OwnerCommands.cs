using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Modules.Common;

namespace SolarisBot.Discord.Modules.Owner
{
    [Module("owner"), Group("owner", "[OWNER ONLY] Configure Solaris"), DefaultMemberPermissions(GuildPermission.Administrator), RequireOwner]
    public sealed class OwnerCommands : SolarisInteractionModuleBase
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OwnerCommands> _logger;
        internal OwnerCommands(IServiceProvider services, ILogger<OwnerCommands> logger)
        {
            _services = services;
            _logger = logger;
        }

        [SlashCommand("set-status", "Set the status of the bot")]
        public async Task SetStatusAsync(string status)
        {
            _logger.LogDebug("{intTag} Setting discord client status to {discordStatus}", GetIntTag(), status);
            var config = _services.GetRequiredService<BotConfig>();
            config.DefaultStatus = status;

            if (!config.SaveAt(Utils.PathConfigFile))
            {
                await Interaction.ReplyErrorAsync("Unable to save new status in config file");
                return;
            }

            var client = _services.GetRequiredService<DiscordSocketClient>();
            await client.SetGameAsync(status);
            _logger.LogInformation("{intTag} Set discord client status to {discordStatus}", GetIntTag(), status);
            await Interaction.ReplyAsync($"Status set to \"{status}\"");
        }
    }
}
