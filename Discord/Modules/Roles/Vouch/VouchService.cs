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
    internal class VouchService
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
        internal async Task<OneOf<Success<List<DbVouchAction>>, Error<string>>> GetVouchHistoryAsync(IGuild guild, ulong userId, int maxDepth)
        {
            if (maxDepth < 1)
                return new Error<string>(StandardError.InvalidParameter("max depth"));

            using var dbCtx = _dbService.GetContext();

            var query = $"SELECT * FROM VouchActions WHERE GuildId = {guild.Id} AND TargetUserId = {userId} ORDER BY VouchedAt DESC";
            var firstVouch = await dbCtx.VouchActions.FromSqlRaw(query).FirstOrDefaultAsync();
            if (firstVouch is null)
                return new Error<string>(StandardError.NoResults);
            
            var vouchHistory = new List<DbVouchAction>() { firstVouch };
            while (vouchHistory.Count < maxDepth)
            {
                query = $"SELECT * FROM VouchActions WHERE GuildId = {guild.Id} AND TargetUserId = {vouchHistory[^1].ExecutingUserId} AND VouchedAt < {vouchHistory[^1].VouchedAt} ORDER BY VouchedAt DESC";
                var previousVouch = await dbCtx.VouchActions.FromSqlRaw(query).FirstOrDefaultAsync();
                if (previousVouch is null)
                    break;
                vouchHistory.Add(previousVouch);
            }

            return new Success<List<DbVouchAction>>(vouchHistory);
        }

        /// <summary>
        /// Gets vouching information for a user
        /// </summary>
        /// <param name="guild">Guild to search</param>
        /// <param name="userId">User to search</param>
        /// <returns>A tuple of user being vouched, user vouching others and count of vouched for on success / Error string</returns>
        internal async Task<OneOf<Success<(DbVouchAction?, List<DbVouchAction>, int)>, Error<string>>> GetVouchInfoAsync(IGuild guild, ulong userId, int limit, bool includeMissing = false)
        {
            if (limit < 1)
                return new Error<string>(StandardError.InvalidParameter("limit"));

            using var dbCtx = _dbService.GetContext();

            var query = $"SELECT * FROM VouchActions WHERE GuildId = {guild.Id} AND TargetUserId = {userId} ORDER BY VouchedAt DESC";
            var vouchedBy = await dbCtx.VouchActions.FromSqlRaw(query).FirstOrDefaultAsync();

            query = $"SELECT * FROM VouchActions WHERE GuildId = {guild.Id} AND ExecutingUserId = {userId} ORDER BY VouchedAt DESC";
            var hasVouchedCount = await dbCtx.VouchActions.FromSqlRaw(query).CountAsync();
            var hasVouched = await dbCtx.VouchActions.FromSqlRaw(query).Take(limit).ToListAsync();

            if (!includeMissing)
            {
                var hasVouchedFiltered = new List<DbVouchAction>();
                foreach (var vouchAction in hasVouched)
                {
                    if (guild.GetUserAsync(vouchAction.TargetUserId) is not null)
                        hasVouchedFiltered.Add(vouchAction);
                }
                hasVouched = hasVouchedFiltered;
            }

            if (vouchedBy is null && hasVouched.Count == 0)
                return new Error<string>(StandardError.NoResults);
            
            return new Success<(DbVouchAction?, List<DbVouchAction>, int)>((vouchedBy, hasVouched, hasVouchedCount));
        }
        #endregion
    }
}
