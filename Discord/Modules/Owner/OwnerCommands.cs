using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Services;
using System.Text;

namespace SolarisBot.Discord.Modules.Owner
{
    [Module("owner"), Group("owner", "[OWNER ONLY] Configure Solaris"), DefaultMemberPermissions(GuildPermission.Administrator), RequireOwner]
    public sealed class OwnerCommands : SolarisInteractionModuleBase
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<OwnerCommands> _logger;
        private readonly StatisticsService _stats;

        internal OwnerCommands(IServiceProvider services, ILogger<OwnerCommands> logger, StatisticsService stats)
        {
            _services = services;
            _logger = logger;
            _stats = stats;
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

        [SlashCommand("stats", "List some statistics")]
        public async Task StatsAsync()
        {
            var commandsTotal = _stats.CommandsFailed + _stats.CommandsExecuted;
            var timeSinceStartup = DateTime.Now - _stats.TimeStarted;
            var totalMinutes = Math.Max(timeSinceStartup.TotalMinutes, 1);

            var sb = new StringBuilder($"Uptime: **{timeSinceStartup:d\\:hh\\:mm\\:ss}**\n");
            double cmdPerMin = commandsTotal / totalMinutes;
            sb.AppendLine($"Commands: **{commandsTotal}** *({Math.Round(cmdPerMin, 2)}/min)*");
            if (commandsTotal > 0)
            {
                double execPercent = _stats.CommandsExecuted / commandsTotal * 100;
                double execPerMin = _stats.CommandsExecuted / totalMinutes;
                sb.AppendLine($"- Success: **{_stats.CommandsExecuted}** *({Math.Round(execPercent, 2)}% | {Math.Round(execPerMin, 2)}/min)*");
                double failPercent = _stats.CommandsFailed / commandsTotal * 100;
                double failPerMin = _stats.CommandsFailed / totalMinutes;
                sb.AppendLine($"- Failed: **{_stats.CommandsFailed}** *({Math.Round(failPercent, 2)}% | {Math.Round(failPerMin, 2)}/min)*");
            }

            await Interaction.ReplyAsync("Statistics", sb.ToString());
        }
    }
}
