using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles.AutoRole
{
    [Module("roles/autorole"), AutoLoadService]
    internal sealed class AutoRoleService
    {
        private readonly ILogger<AutoRoleService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseService _dbService;

        public AutoRoleService(ILogger<AutoRoleService> logger, DiscordSocketClient client, DatabaseService dbService)
        {
            _client = client;
            _dbService = dbService;
            _logger = logger;

            _client.UserJoined += ApplyAutoRoleAsync;

        }

        #region Commands
        /// <summary>
        /// Configures auto role in guild
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="role">Role to apply</param>
        /// <returns>GuildConfig on success / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigAutoRoleAsync(IGuild guild, IRole? role)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);
            dbGuild.AutoRoleId = role?.Id ?? ulong.MinValue;

            _logger.LogTrace("Setting auto-role to role {role} for guild {guild}", role?.Log() ?? "0", guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting auto-role to role {role} for guild {guild}", role?.Log() ?? "0", guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogDebug("Set auto-role to role {role} for guild {guild}", role?.Log() ?? "0", guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }
        #endregion

        #region OnJoin
        /// <summary>
        /// Automatically applies a role to a user on join when set up
        /// </summary>
        private async Task ApplyAutoRoleAsync(SocketGuildUser user)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetGuildByIdAsync(user.Guild.Id);
            if (dbGuild is null || dbGuild.AutoRoleId == ulong.MinValue)
                return;

            if (user.Guild.FindRole(dbGuild.AutoRoleId) is null)
            {
                _logger.LogDebug("Could not locate AutoRole for guild {guild} with id {roleId}", dbGuild, dbGuild.AutoRoleId); //todo: [REFACTOR] Tweak priorities for logging
                return;
            }

            try
            {
                _logger.LogTrace("Applying auto-role {auto-role} to user {user} in guild {guild}", dbGuild.AutoRoleId, user.Log(), user.Guild.Log());
                await user.AddRoleAsync(dbGuild.AutoRoleId);
                _logger.LogDebug("Applied auto-role {auto-role} to user {user} in guild {guild}", dbGuild.AutoRoleId, user.Log(), user.Guild.Log());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to apply auto-role {auto-role} to user {user} in guild {guild}", dbGuild.AutoRoleId, user.Log(), user.Guild.Log());
            }
        }
        #endregion
    }
}
