using Discord;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Utility
{
    [AutoLoadService]
    internal class MiscConfigService
    {
        private readonly ILogger<MiscConfigService> _logger;
        private readonly DatabaseService _dbService;
        public MiscConfigService(ILogger<MiscConfigService> logger, DatabaseService dbService)
        {
            _dbService = dbService;
            _logger = logger;
        }

        /// <summary>
        /// Sets error dms in a guild
        /// </summary>
        /// <param name="guild">guild to configure</param>
        /// <param name="enabled">Is feature enabled?</param>
        /// <returns>GuildConfig on succes / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> SetErrorDmAsync(IGuild guild, bool enabled)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);

            dbGuild.DisableErrorDm = !enabled;

            _logger.LogDebug("Setting error dms to enabled={enabled} in guild {guild}", enabled, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed error dms to enabled={enabled} in guild {guild}", enabled, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Set error dms to enabled={enabled} in guild {guild}", enabled, guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }
    }
}
