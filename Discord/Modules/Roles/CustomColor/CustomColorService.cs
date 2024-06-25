using Color = Discord.Color;
using Discord;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using Discord.WebSocket;
using SolarisBot.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Discord.Modules.Roles.CustomColor
{
    [Module("roles/customcolor"), AutoLoadService]
    internal class CustomColorService
    {
        private readonly ILogger<CustomColorService> _logger;
        private readonly DatabaseService _dbService;

        public CustomColorService(ILogger<CustomColorService> logger, DatabaseService dbService)
        {
            _dbService = dbService;
            _logger = logger;
        }

        #region Commands
        /// <summary>
        /// Creates and applies a custom color role
        /// </summary>
        /// <param name="guild">Guild to create role in</param>
        /// <param name="user">User to apply role to</param>
        /// <param name="color">Color of role</param>
        /// <returns>Created role on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<IRole>, Error<string>, Error<Exception>>> CreateCustomColorRole(IGuild guild, IUser user, string identifier, Color color)
        {
            if (!DiscordUtils.IsIdentifierValid(identifier))
                return new Error<string>(StandardError.InvalidIdentifier(identifier));

            if (user is not SocketGuildUser gUser)
                return new Error<string>(StandardError.FailedConversion("executing user", "SocketGuildUser"));

            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetGuildByIdAsync(guild.Id);

            if (dbGuild is null || dbGuild.CustomColorPermissionRoleId == ulong.MinValue)
                return new Error<string>(StandardError.DisabledFeature("Custom color"));
            if (guild.FindRole(dbGuild.CustomColorPermissionRoleId) is null)
                return new Error<string>(StandardError.DeletedRole("Custom color"));
            if (gUser.FindRole(dbGuild.CustomColorPermissionRoleId) is null)
                return new Error<string>(StandardError.RoleRequired(dbGuild.CustomColorPermissionRoleId));

            var customColorRoleDb = await dbCtx.CustomColorRoles.ForGuild(guild.Id).ForUser(user.Id).FirstOrDefaultAsync();

            var customColorRoleDiscord = customColorRoleDb is null ? null : guild.FindRole(customColorRoleDb.RoleId);
            var roleName = string.IsNullOrWhiteSpace(dbGuild.CustomColorIndicator) ? identifier : $"{dbGuild.CustomColorIndicator} {identifier}";
            if (customColorRoleDiscord is null)
            {
                try
                {    
                    _logger.LogDebug("Creating custom color role {roleName} for user {user} in guild {guild}", roleName, gUser.Log(), guild.Log());
                    customColorRoleDiscord = await guild.CreateRoleAsync(roleName, color: color, isMentionable: false);
                    _logger.LogInformation("Created custom color role {role} for user {user} in guild {guild}", customColorRoleDiscord.Log(), gUser.Log(), guild.Log());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed creating custom color role {roleName} for user {user} in guild {guild}", roleName, gUser.Log(), guild.Log());
                    return new Error<Exception>(ex);
                }
            }
            else
            {
                try
                {
                    _logger.LogDebug("Modifying custom color role {role} for user {user} in guild {guild}", customColorRoleDiscord.Log(), gUser.Log(), guild.Log());
                    await customColorRoleDiscord.ModifyAsync(x => { x.Color = color; x.Name = roleName; });
                    _logger.LogInformation("Modified custom color role {role} for user {user} in guild {guild}", customColorRoleDiscord.Log(), gUser.Log(), guild.Log());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed modifying custom color role {role} for user {user} in guild {guild}", customColorRoleDiscord.Log(), gUser.Log(), guild.Log());
                    return new Error<Exception>(ex);
                }
            }

            _logger.LogDebug("Saving custom color role {role} for user {user} in guild {guild} in DB", customColorRoleDiscord.Log(), gUser.Log(), guild.Log());
            customColorRoleDb ??= new()
            {
                GuildId = guild.Id,
                UserId = gUser.Id,
            };
            customColorRoleDb.RoleId = customColorRoleDiscord.Id;
            dbCtx.CustomColorRoles.Update(customColorRoleDb);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed saving custom color role {role} for user {user} in guild {guild} in DB", customColorRoleDiscord.Log(), gUser.Log(), guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Saved custom color role {role} for user {user} in guild {guild} in DB", customColorRoleDiscord.Log(), gUser.Log(), guild.Log());

            if (!gUser.Roles.Contains(customColorRoleDiscord))
            {
                try
                {
                    _logger.LogDebug("Adding custom color role {role} to user {user} in guild {guild}", customColorRoleDiscord.Log(), gUser.Log(), guild.Log());
                    await gUser.AddRoleAsync(customColorRoleDiscord);
                    _logger.LogInformation("Added custom color role {role} to user {user} in guild {guild}", customColorRoleDiscord.Log(), gUser.Log(), guild.Log());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed adding custom color role {role} to user {user} in guild {guild}", customColorRoleDiscord.Log(), gUser.Log(), guild.Log());
                    return new Error<Exception>(ex);
                }
            }

            return new Success<IRole>(customColorRoleDiscord);
        }

        /// <summary>
        /// Deletes a custom color role
        /// </summary>
        /// <param name="guild">Guild to delete role from</param>
        /// <param name="user">Owner of role</param>
        /// <returns>Success / Error string / Exception</returns>
        internal async Task<OneOf<Success, Error<string>, Error<Exception>>> DeleteCustomColorRole(IGuild guild, IUser user)
        {
            using var dbCtx = _dbService.GetContext();
            var dbRole = await dbCtx.CustomColorRoles.ForGuild(guild.Id).ForUser(user.Id).FirstOrDefaultAsync();
            if (dbRole is null)
                return new Error<string>(StandardError.NoResults);

            var discordRole = guild.FindRole(dbRole.RoleId);
            if (discordRole is null)
                return new Error<string>(StandardError.NoResults);

            try
            {
                _logger.LogDebug("Deleting custom color role {role} from {guild}", discordRole.Log(), guild.Log());
                await discordRole.DeleteAsync();
                _logger.LogInformation("Deleted custom color role {role} from {guild}", discordRole.Log(), guild.Log());
                return new Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed deleting custom color role {role} from {guild}", discordRole.Log(), guild.Log());
                return new Error<Exception>(ex);
            }
        }
        #endregion

        #region Commands - Config
        /// <summary>
        /// Configures custom colors in a guild
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="role">Permission role</param>
        /// <returns>GuildConfig on success / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<string>, Error<Exception>>> ConfigCustomColorAsync(IGuild guild, IRole? role, string indicator)
        {
            if (!DiscordUtils.IsIdentifierValid(indicator))
                return new Error<string>(StandardError.InvalidIdentifier(indicator));

            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);
            dbGuild.CustomColorPermissionRoleId = role?.Id ?? ulong.MinValue;
            dbGuild.CustomColorIndicator = indicator;

            _logger.LogDebug("Setting custom colors to role={role} in guild {guild}", role?.Log() ?? "0", guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting custom colors to role={role} in guild {guild}", role?.Log() ?? "0", guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Set custom colors to role={role} in guild {guild}", role?.Log() ?? "0", guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }

        /// <summary>
        /// Deletes all custom color roles within a guild
        /// </summary>
        /// <param name="guild">Guild for deletion</param>
        /// <returns>Array of deleted roles on success / none / exception</returns>
        internal async Task<OneOf<Success<IRole[]>, Error<string>, Error<Exception>>> DeleteCustomColorRolesForGuildAsync(IGuild guild)
        {
            var roles = guild.Roles.Where(x => x.Name.StartsWith(DiscordUtils.CustomColorRolePrefix)).ToArray();
            if (roles.Length == 0)
                return new Error<string>(StandardError.NoResults);

            try
            {
                _logger.LogDebug("Deleting {roleCount} custom color roles in guild {guild}", roles.Length, guild.Log());
                foreach (var role in roles)
                    await role.DeleteAsync();
                _logger.LogInformation("Deleted {roleCount} custom color roles in guild {guild}", roles.Length, guild.Log());
                return new Success<IRole[]>(roles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed deleting {roleCount} custom color roles in guild {guild}", roles.Length, guild.Log());
                return new Error<Exception>(ex);
            }
        }

        /// <summary>
        /// Deletes all custom color roles without owner from a guild
        /// </summary>
        /// <param name="guild">Guild to delete from</param>
        /// <returns>Array of deleted roles on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<IRole[]>, Error<string>, Error<Exception>>> DeleteOwnerlessCustomColorRolesForGuildAsync(IGuild guild) //todo: rework
        {
            var roles = guild.Roles.Where(x => x.Name.StartsWith(DiscordUtils.CustomColorRolePrefix));
            if (!roles.Any())
                return new Error<string>(StandardError.NoResults);

            var guildUsers = await guild.GetUsersAsync();
            var guildUserStringIds = guildUsers.Select(x => x.Id.ToString());
            var rolesWithoutOwner = roles.Where(x => !guildUserStringIds.Contains(GetIdFromCustomColorRoleName(x.Name))).ToArray();

            if (rolesWithoutOwner.Length == 0)
                return new Error<string>(StandardError.NoResults);

            try
            {
                _logger.LogDebug("Deleting {roleCount} custom color roles without owner in guild {guild}", rolesWithoutOwner.Length, guild.Log());
                foreach (var role in rolesWithoutOwner)
                    await role.DeleteAsync();
                _logger.LogInformation("Deleted {roleCount} custom color roles without owner in guild {guild}", rolesWithoutOwner.Length, guild.Log());
                return new Success<IRole[]>(rolesWithoutOwner);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed deleting {roleCount} custom color roles without owner in guild {guild}", rolesWithoutOwner.Length, guild.Log());
                return new Error<Exception>(ex);
            }
        }

        /// <summary>
        /// Gets the ID od a user from the name of a custom color role
        /// </summary>
        /// <param name="customColorRoleName">Role name</param>
        /// <returns>UserId as string</returns>
        private static string GetIdFromCustomColorRoleName(string customColorRoleName) //todo: remove
            => customColorRoleName.Replace($"{DiscordUtils.CustomColorRolePrefix} ", string.Empty);
        #endregion
    }
}
