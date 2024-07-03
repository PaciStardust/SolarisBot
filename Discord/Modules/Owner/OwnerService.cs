using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common.Attributes;
using System.Diagnostics;
using System.Text;

namespace SolarisBot.Discord.Modules.Owner
{
    [Module("owner"), AutoLoadService]
    internal class OwnerService
    {
        private readonly BotConfig _botConfig;
        private readonly ILogger<OwnerService> _logger;
        private readonly DatabaseService _dbService;
        private readonly DiscordSocketClient _client;

        public OwnerService(BotConfig botConfig, ILogger<OwnerService> logger, DatabaseService dbService, DiscordSocketClient client)
        {
            _botConfig = botConfig;
            _logger = logger;
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
        /// Generates a string from the DB
        /// </summary>
        /// <returns>Generated string</returns>
        internal async Task<string> GetStatsString()
        {
            var sessionStarted = Process.GetCurrentProcess().StartTime;
            var timeSinceStartup = DateTime.Now - sessionStarted;
            var sb = new StringBuilder($"Uptime: **{timeSinceStartup:d\\:hh\\:mm\\:ss}**\n");

            using var dbCtx = _dbService.GetContext();

            var interactionsTotal = await dbCtx.InteractionRecords.CountAsync();
            if (interactionsTotal > 0)
            {
                sb.AppendLine($"Interactions: **{interactionsTotal}**");

                var interactionsSuccess = await dbCtx.InteractionRecords.CountAsync(x => x.Success);
                double successPercent = interactionsSuccess / interactionsTotal * 100;
                sb.AppendLine($"- Success: **{interactionsSuccess}** *({Math.Round(successPercent, 2)}%)*");

                var interactionsFailed = interactionsTotal - interactionsSuccess;
                double failedPercent = interactionsFailed / interactionsTotal * 100;
                sb.AppendLine($"- Failed: **{interactionsFailed}** *({Math.Round(failedPercent, 2)}%)*");
                double? failureRate = interactionsSuccess > 0 ? interactionsFailed / interactionsSuccess : null;
                sb.AppendLine($"- F/S: **{(failureRate.HasValue ? failureRate.Value.ToString() : "N/A")}**");

                var sessionStartedUnix = Convert.ToUInt64(((DateTimeOffset)sessionStarted.ToUniversalTime()).ToUnixTimeSeconds());
                var sessionQuery = $"SELECT COUNT(InteractionRecordId) FROM InteractionRecords WHERE InteractionCreatedAt >= {sessionStartedUnix}";
                var interactionsTotalSession = await dbCtx.Database.SqlQueryRaw<int>(sessionQuery).FirstOrDefaultAsync();
                if (interactionsTotalSession > 0)
                {
                    double sessionPercent = interactionsTotalSession / interactionsTotal * 100;
                    sb.AppendLine($"\nSession: **{interactionsTotalSession}** *({Math.Round(sessionPercent, 2)}%)*");

                    sessionQuery += " AND success = 1";
                    var interactionsSuccessSession = await dbCtx.Database.SqlQueryRaw<int>(sessionQuery).FirstOrDefaultAsync();

                    var minutesSession = Math.Max(timeSinceStartup.TotalMinutes, 1);
                    var interactionsPerMinuteSession = interactionsTotalSession / minutesSession;

                    double successPercentSession = interactionsSuccessSession / interactionsTotalSession * 100;
                    double successPerMinuteSession = interactionsTotalSession / minutesSession;
                    double successPercentSessionOfTotal = interactionsSuccessSession / interactionsSuccess * 100;
                    sb.AppendLine($"- Success: **{interactionsSuccessSession}** *({Math.Round(successPercentSession, 2)}% | {Math.Round(successPerMinuteSession, 2)}/min | {Math.Round(successPercentSessionOfTotal, 2)}%t)*");

                    var interactionsFailedSession = interactionsTotalSession - interactionsSuccessSession;
                    double failedPercentSession = interactionsFailedSession / interactionsTotalSession * 100;
                    double failedPerMinuteSession = interactionsFailedSession / minutesSession;
                    double failedPercentSessionOfTotal = interactionsFailedSession / interactionsFailed * 100;
                    sb.AppendLine($"- Failed: **{interactionsFailedSession}** *({Math.Round(failedPercentSession, 2)}% | {Math.Round(failedPerMinuteSession, 2)}/min | {Math.Round(failedPercentSessionOfTotal, 2)}%t)*");
                    double? failureRateSession = interactionsSuccessSession > 0 ? interactionsFailedSession / interactionsSuccessSession : null;
                    sb.AppendLine($"- F/S: **{(failureRateSession.HasValue ? failureRateSession.Value.ToString() : "N/A")}**");
                }
                else
                    sb.AppendLine("\nNo interactions created this session");
            }
            else
                sb.AppendLine("No interactions created");

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
