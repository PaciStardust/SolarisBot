using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Database.Models;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles.Vouch
{
    [Module("roles/vouch"), AutoLoadService]
    internal class VouchService //todo: [FEATURE] Vouch Tree
    {
        private readonly ILogger<VouchService> _logger;
        private readonly DatabaseService _dbService;
        public VouchService(ILogger<VouchService> logger, DatabaseService dbService)
        {
            _dbService = dbService;
            _logger = logger;
        }

        #region Commands
        /// <summary>
        /// Vouches for a user in a guild
        /// </summary>
        /// <param name="guild">Guild for vouching</param>
        /// <param name="executingUser">User executing vouch</param>
        /// <param name="targetUser">User targeted by vouch</param>
        /// <returns>Success / Error string / Exception</returns>
        internal async Task<OneOf<Success, Error<string>, Error<Exception>>> VouchUserAsync(IGuild guild, IUser executingUser, IUser targetUser)
        {
            if (executingUser is not SocketGuildUser executingGuildUser)
                return new Error<string>(StandardError.FailedConversion("executing user", "SocketGuildUser"));
            if (targetUser is not SocketGuildUser targetGuildUser)
                return new Error<string>(StandardError.FailedConversion("target user", "SocketGuildUser"));

            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetGuildByIdAsync(guild.Id);

            if (dbGuild is null || !dbGuild.VouchingOn)
                return new Error<string>(StandardError.DisabledFeature("Vouching"));

            if (guild.FindRole(dbGuild.VouchPermissionRoleId) is null)
                return new Error<string>(StandardError.DeletedRole("Vouch permission"));
            if (executingGuildUser.FindRole(dbGuild.VouchPermissionRoleId) is null)
                return new Error<string>($"You do not have the required role <@&{dbGuild.VouchPermissionRoleId}>");
            if (guild.FindRole(dbGuild.VouchRoleId) is null)
                return new Error<string>(StandardError.DeletedRole("Vouch"));
            if (targetGuildUser.FindRole(dbGuild.VouchRoleId) is not null)
                return new Error<string>($"{targetGuildUser.Mention} has already been vouched");

            _logger.LogDebug("Recording vouch of user {targetUserData}, has been vouched({vouchRoleId}) for in {guild} by {userData}", targetGuildUser.Log(), dbGuild.VouchRoleId, guild.Log(), executingGuildUser.Log());
            var vouchAction = new DbVouchAction()
            {
                GuildId = guild.Id,
                ExecutingUserId = executingGuildUser.Id,
                TargetUserId = targetGuildUser.Id,
            };
            dbCtx.VouchActions.Add(vouchAction);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed recording vouch of user {targetUserData}, has been vouched({vouchRoleId}) for in {guild} by {userData}", targetGuildUser.Log(), dbGuild.VouchRoleId, guild.Log(), executingGuildUser.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Recorded vouch of user {targetUserData}, has been vouched({vouchRoleId}) for in {guild} by {userData}", targetGuildUser.Log(), dbGuild.VouchRoleId, guild.Log(), executingGuildUser.Log());

            _logger.LogDebug("Giving vouch role to user {targetUserData}, has been vouched({vouchRoleId}) for in {guild} by {userData}", targetGuildUser.Log(), dbGuild.VouchRoleId, guild.Log(), executingGuildUser.Log());
            try
            {
                await targetGuildUser.AddRoleAsync(dbGuild.VouchRoleId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed giving vouch role to user {targetUserData}, has been vouched({vouchRoleId}) for in {guild} by {userData}", targetGuildUser.Log(), dbGuild.VouchRoleId, guild.Log(), executingGuildUser.Log());
                return new Error<Exception>(ex);
            }
            _logger.LogInformation("Gave vouch role to user {targetUserData}, has been vouched({vouchRoleId}) for in {guild} by {userData}", targetGuildUser.Log(), dbGuild.VouchRoleId, guild.Log(), executingGuildUser.Log());
            return new Success();
        }
        #endregion

        #region Commands - Config
        /// <summary>
        /// Configure vouching for a guild
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="permission">Vouch permission role</param>
        /// <param name="vouch">Vouch role</param>
        /// <returns>GuildConfig on success / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigVouchingAsync(IGuild guild, IRole? permission, IRole? vouch)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);

            dbGuild.VouchPermissionRoleId = permission?.Id ?? ulong.MinValue;
            dbGuild.VouchRoleId = vouch?.Id ?? ulong.MinValue;

            _logger.LogDebug("Setting vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", permission?.Log() ?? "0", vouch?.Log() ?? "0", guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", permission?.Log() ?? "0", vouch?.Log() ?? "0", guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Set vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", permission?.Log() ?? "0", vouch?.Log() ?? "0", guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }

        /// <summary>
        /// Gets the vouching history of a user
        /// </summary>
        /// <param name="guild">Guild to search</param>
        /// <param name="userId">User to search</param>
        /// <param name="maxDepth">Maximum search depth</param>
        /// <returns>A list of vouches in reverse chronological order on success / Error string</returns>
        internal async Task<OneOf<Success<List<DbVouchAction>>, Error<string>>> GetVouchHistoryAsync(IGuild guild, ulong userId, int maxDepth) //todo: implement
        {
            if (maxDepth < 1)
                return new Error<string>("Maximum depth for search can not be under 1");

            using var dbCtx = _dbService.GetContext();

            var firstVouch = await dbCtx.VouchActions.ForGuild(guild.Id).Where(x => x.TargetUserId == userId).OrderByDescending(x => x.VouchedAt).FirstOrDefaultAsync();
            if (firstVouch is null)
                return new Error<string>(StandardError.NoResults);
            
            var vouchHistory = new List<DbVouchAction>() { firstVouch };
            while (vouchHistory.Count < maxDepth)
            {
                var currentLastVouch = vouchHistory[^1];
                var previousVouch = await dbCtx.VouchActions.ForGuild(guild.Id).Where(x => x.TargetUserId == currentLastVouch.ExecutingUserId && x.VouchedAt < currentLastVouch.VouchedAt).OrderByDescending(x => x.VouchedAt).FirstOrDefaultAsync();
                if (previousVouch is null)
                    break;
                vouchHistory.Add(previousVouch);
            }

            return new Success<List<DbVouchAction>>(vouchHistory);
        }
        #endregion
    }
}
