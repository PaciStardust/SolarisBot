using Bogus;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles.UtilityRoles
{
    [Module("roles/magic", "roles/vouch", "roles/quarantine"), AutoLoadService]
    internal class UtilityRoleService
    {
        private readonly ILogger<UtilityRoleService> _logger;
        private readonly DatabaseService _dbService;
        internal UtilityRoleService(ILogger<UtilityRoleService> logger, DatabaseService dbService)
        {
            _dbService = dbService;
            _logger = logger;
        }

        #region Magic
        /// <summary>
        /// Configures magic in a guild
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="role">Role for magic</param>
        /// <param name="timeout">Timeout for magic</param>
        /// <param name="renaming">Renaming for role</param>
        /// <returns>GuildConfig on success / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigMagicAsync(IGuild guild, IRole? role, ulong timeout, bool renaming)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);

            dbGuild.MagicRoleId = role?.Id ?? ulong.MinValue;
            dbGuild.MagicRoleNextUse = ulong.MinValue;
            dbGuild.MagicRoleTimeout = timeout >= ulong.MinValue ? timeout : ulong.MinValue;
            dbGuild.MagicRoleRenameOn = renaming;

            _logger.LogDebug("Setting magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", role?.Log() ?? "0", dbGuild.MagicRoleTimeout, dbGuild.MagicRoleRenameOn, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", role?.Log() ?? "0", dbGuild.MagicRoleTimeout, dbGuild.MagicRoleRenameOn, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Set magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", role?.Log() ?? "0", dbGuild.MagicRoleTimeout, dbGuild.MagicRoleRenameOn, guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }

        /// <summary>
        /// Uses magic in a guild
        /// </summary>
        /// <param name="guild">Guild to use magic in</param>
        /// <returns>Modified role on success / DeletedRole / Error string / Exception</returns>
        internal async Task<OneOf<Success<IRole>, DeletedRole<string>, Error<string>, Error<Exception>>> UseMagicAsync(IGuild guild)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetGuildByIdAsync(guild.Id);

            if (dbGuild is null || dbGuild.MagicRoleId == ulong.MinValue)
                return new Error<string>("Magic is not enabled in this guild");

            var role = guild.FindRole(dbGuild.MagicRoleId);
            if (role is null)
                return new DeletedRole<string>("Magic");

            var currentTime = Utils.GetCurrentUnix();
            if (currentTime < dbGuild.MagicRoleNextUse)
                return new Error<string>($"There is currently not enough mana to use magic, please wait until <t:{dbGuild.MagicRoleNextUse}:R>");

            var faker = Utils.Faker;
            var color = new Color(faker.Random.Byte(), faker.Random.Byte(), faker.Random.Byte());

            try
            {
                _logger.LogDebug("Using Magic({magicRoleId}) in guild {guild} - Updating role", dbGuild.MagicRoleId, guild.Log());
                await role.ModifyAsync(x =>
                {
                    x.Name = dbGuild.MagicRoleRenameOn ? GenerateMagicName(faker) : x.Name;
                    x.Color = color;
                });
                _logger.LogInformation("Using Magic({magicRoleId}) in guild {guild} - Updated role", dbGuild.MagicRoleId, guild.Log());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed using Magic({magicRoleId}) in guild {guild} - Failed updating role", dbGuild.MagicRoleId, guild.Log());
                return new Error<Exception>(ex);
            }

            _logger.LogDebug("Using Magic({magicRoleId}) in guild {guild} - Updating next use to {nextUse}", dbGuild.MagicRoleId, guild.Log(), dbGuild.MagicRoleNextUse);
            dbGuild.MagicRoleNextUse = currentTime + dbGuild.MagicRoleTimeout;
            dbCtx.GuildConfigs.Update(dbGuild);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed using Magic({magicRoleId}) in guild {guild} - Failed updating next use to {nextUse}", dbGuild.MagicRoleId, guild.Log(), dbGuild.MagicRoleNextUse);
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Used Magic({magicRoleId}) in guild {guild} - Updated next use to {nextUse}", dbGuild.MagicRoleId, guild.Log(), dbGuild.MagicRoleNextUse);
            return new Success<IRole>(role);
        }

        /// <summary>
        /// Generates a random name for the magic role
        /// </summary>
        private static string GenerateMagicName(Faker faker)
        {
            var num = faker.Random.Byte(0, 3);
            if (num == 0)
            {
                var adjective = faker.Hacker.Adjective();
                return $"{adjective[0].ToString().ToUpper()}{adjective[1..]} {faker.Name.FirstName()}";
            }
            else if (num == 1)
                return $"{faker.Commerce.ProductAdjective()} {faker.Name.FirstName()}";
            else
                return faker.Commerce.ProductName();
        }
        #endregion

        #region Vouching
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
        /// Vouches for a user in a guild
        /// </summary>
        /// <param name="guild">Guild for vouching</param>
        /// <param name="executingUser">User executing vouch</param>
        /// <param name="targetUser">User targeted by vouch</param>
        /// <returns>Success / DeletedRole / Error string / Exception</returns>
        internal async Task<OneOf<Success, DeletedRole<string>, Error<string>, Error<Exception>>> VouchUserAsync(IGuild guild, IUser executingUser, IUser targetUser)
        {
            if (executingUser is not SocketGuildUser executingGuildUser)
                return new Error<string>("Could not convert executing user to socket guild user");
            if (targetUser is not SocketGuildUser targetGuildUser)
                return new Error<string>("Could not convert target user to socket guild user");

            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetGuildByIdAsync(guild.Id);

            if (dbGuild is null || !dbGuild.VouchingOn)
                return new Error<string>("Vouching is not enabled in this guild");

            if (guild.FindRole(dbGuild.VouchPermissionRoleId) is null) //todo: [LOGGING] Is logging needed if one of these fails?
                return new DeletedRole<string>("Vouch permission");
            if (executingGuildUser.FindRole(dbGuild.VouchPermissionRoleId) is null)
                return new Error<string>($"You do not have the required role <@&{dbGuild.VouchPermissionRoleId}>");
            if (guild.FindRole(dbGuild.VouchRoleId) is null)
                return new DeletedRole<string>("Vouch");
            if (targetGuildUser.FindRole(dbGuild.VouchRoleId) is not null)
                return new Error<string>($"{targetGuildUser.Mention} has already been vouched");

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

        #region Quarantine
        /// <summary>
        /// Configures quarantine in a guild
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="role">Role for quarantine</param>
        /// <returns>GuildConfig on success / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigQuarantineAsync(IGuild guild, IRole? role)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);

            dbGuild.QuarantineRoleId = role?.Id ?? ulong.MinValue;

            _logger.LogDebug("Setting quarantine to role={role} in guild {guild}", role?.Log() ?? "0", guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting quarantine to role={role} in guild {guild}", role?.Log() ?? "0", guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Set quarantine to role={role} in guild {guild}", role?.Log() ?? "0", guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }

        /// <summary>
        /// Quarantines a user in a guild
        /// </summary>
        /// <param name="guild">Guild to quarantine in</param>
        /// <param name="executingUser">User quarantining</param>
        /// <param name="targetUser">User being quarantined</param>
        /// <returns>Has user been quarantined? / DeletedRole / Error string / Exception</returns>
        internal async Task<OneOf<Success<bool>, DeletedRole<string>, Error<string>, Error<Exception>>> QuarantineUserAsync(IGuild guild, IUser executingUser, IUser targetUser)
        {
            if (executingUser is not SocketGuildUser executingGuildUser)
                return new Error<string>("Could not convert executing user to socket guild user");
            if (targetUser is not SocketGuildUser targetGuildUser)
                return new Error<string>("Could not convert target user to socket guild user");

            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetGuildByIdAsync(guild.Id);

            if (dbGuild is null || dbGuild.QuarantineRoleId == ulong.MinValue)
                return new Error<string>("Quarantine is not enabled in this guild");
            if (guild.FindRole(dbGuild.QuarantineRoleId) is null)
                return new DeletedRole<string>("Quarantine");

            if (targetGuildUser.FindRole(dbGuild.QuarantineRoleId) is not null)
            {
                try
                {
                    _logger.LogDebug("Removing quarantine role from user {targetUserData}, has been removed({quarantineRoleId}) in {guild} by {userData}", targetGuildUser.Log(), dbGuild.QuarantineRoleId, guild.Log(), executingGuildUser.Log());
                    await targetGuildUser.RemoveRoleAsync(dbGuild.QuarantineRoleId);
                    _logger.LogInformation("Removed quarantine role from user {targetUserData}, has been removed({quarantineRoleId}) in {guild} by {userData}", targetGuildUser.Log(), dbGuild.QuarantineRoleId, guild.Log(), executingGuildUser.Log());
                    return new Success<bool>(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed removing quarantine role from user {targetUserData}, has been removed({quarantineRoleId}) in {guild} by {userData}", targetGuildUser.Log(), dbGuild.QuarantineRoleId, guild.Log(), executingGuildUser.Log());
                    return new Error<Exception>(ex);
                }
            }

            try
            {
                _logger.LogDebug("Giving quarantine role to user {targetUserData}, has been quarantined({quarantineRoleId}) in {guild} by {userData}", targetGuildUser.Log(), dbGuild.QuarantineRoleId, guild.Log(), executingGuildUser.Log());
                await targetGuildUser.AddRoleAsync(dbGuild.QuarantineRoleId);
                _logger.LogInformation("Gave quarantine role to user {targetUserData}, has been quarantined({quarantineRoleId}) in {guild} by {userData}", targetGuildUser.Log(), dbGuild.QuarantineRoleId, guild.Log(), executingGuildUser.Log());
                return new Success<bool>(true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed giving quarantine role to user {targetUserData}, has been quarantined({quarantineRoleId}) in {guild} by {userData}", targetGuildUser.Log(), dbGuild.QuarantineRoleId, guild.Log(), executingGuildUser.Log());
                return new Error<Exception>(ex);
            }
        }
        #endregion
    }
}
