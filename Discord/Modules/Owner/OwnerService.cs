using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Services;
using System.Text;

namespace SolarisBot.Discord.Modules.Owner
{
    [Module("owner"), AutoLoadService]
    internal class OwnerService
    {
        private readonly BotConfig _botConfig;
        private readonly ILogger<OwnerService> _logger;
        private readonly StatisticsService _stats;
        private readonly DatabaseService _dbService;
        private readonly DiscordSocketClient _client;

        public OwnerService(BotConfig botConfig, ILogger<OwnerService> logger, StatisticsService stats, DatabaseService dbService, DiscordSocketClient client)
        {
            _botConfig = botConfig;
            _logger = logger;
            _stats = stats;
            _dbService = dbService;
            _client = client;
        }

        #region Commands
        /// <summary>
        /// Set the bots status
        /// </summary>
        /// <param name="status">Status to set</param>
        /// <returns>Succss / Error string / Exception</returns>
        internal async Task<OneOf<Success, Error<string>, Error<Exception>>> SetStatusAsync(string status)
        {
            _logger.LogDebug("Setting discord client status to {discordStatus}", status);
            _botConfig.DefaultStatus = status;

            if (!_botConfig.SaveAt(Utils.PathConfigFile))
                return new Error<string>("Unable to save new status in config file");

            try
            {
                await _client.SetGameAsync(status);
                _logger.LogInformation("Set discord client status to {discordStatus}", status);
                return new Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed setting discord client status to {discordStatus}", status);
                return new Error<Exception>(ex);
            }
        }

        /// <summary>
        /// Generates a string from the statisticsservice
        /// </summary>
        /// <returns>Generated string</returns>
        internal string GetStatsString()
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
            return sb.ToString();
        }

        /// <summary>
        /// Runs a raw SQL string
        /// </summary>
        /// <param name="query">Query to run</param>
        /// <returns>Lines changed on success / Exception </returns>
        internal async Task<OneOf<Success<int>, Error<Exception>>> RunRawSqlAsync(string query)
        {
            _logger.LogWarning("Executing manual RAW run query {query}", query);
            using var dbCtx = _dbService.GetContext();
            try
            {
                var sql = await dbCtx.Database.ExecuteSqlRawAsync(query);
                _logger.LogWarning("Executed manual RAW run query {query}", query);
                return new Success<int>(sql);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed executing manual RAW run query {query}", query);
                return new Error<Exception>(ex);
            }
        }
        #endregion
    }
}
