using Color = Discord.Color;
using Discord;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using Discord.WebSocket;
using Microsoft.VisualBasic;

namespace SolarisBot.Discord.Modules.Roles.CustomColor
{
    [Module("roles/customcolor"), AutoLoadService]
    internal class CustomColorService
    {
        private readonly ILogger<CustomColorService> _logger;
        private readonly DatabaseService _dbService;

        internal CustomColorService(ILogger<CustomColorService> logger, DatabaseService dbService)
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
        /// <returns>Created role on success / Name of deleted role / Error string / Exception</returns>
        internal async Task<OneOf<Success<IRole>, DeletedRole<string>, Error<string>, Error<Exception>>> CreateCustomColorRole(IGuild guild, IUser user, Color color)
        {
            if (user is not SocketGuildUser gUser)
                return new Error<string>("Could not convert user to socket guild user");

            var generatedRoleName = DiscordUtils.GetCustomColorRoleName(gUser);
            var customColorRole = guild.Roles.FirstOrDefault(x => x.Name == generatedRoleName);

            if (customColorRole is null)
            {
                using var dbCtx = _dbService.GetContext();
                var permissionRole = (await dbCtx.GetGuildByIdAsync(guild.Id))?.CustomColorPermissionRoleId;
                if (permissionRole is null || permissionRole == ulong.MinValue)
                    return new Error<string>("Custom color roles are not enabled in this guild");
                if (guild.FindRole(permissionRole.Value) is null)
                    return new DeletedRole<string>("Custom color");
                if (gUser.FindRole(permissionRole.Value) is null)
                    return new Error<string>($"You do not have the required role <@&{permissionRole}>");

                try
                {
                    _logger.LogDebug("Creating custom color role {roleName} for user {user} in guild {guild}", generatedRoleName, gUser.Log(), guild.Log());
                    customColorRole = await guild.CreateRoleAsync(generatedRoleName, color: color, isMentionable: false);
                    _logger.LogInformation("Created custom color role {role} for user {user} in guild {guild}", customColorRole.Log(), gUser.Log(), guild.Log());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed creating custom color role {roleName} for user {user} in guild {guild}", generatedRoleName, gUser.Log(), guild.Log());
                    return new Error<Exception>(ex);
                }
            }
            else
            {
                try
                {
                    _logger.LogDebug("Modifying custom color role {role} for user {user} in guild {guild}", customColorRole.Log(), gUser.Log(), guild.Log());
                    await customColorRole.ModifyAsync(x => x.Color = color);
                    _logger.LogInformation("Modified custom color role {role} for user {user} in guild {guild}", customColorRole.Log(), gUser.Log(), guild.Log());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed modifying custom color role {role} for user {user} in guild {guild}", customColorRole.Log(), gUser.Log(), guild.Log());
                    return new Error<Exception>(ex);
                }
            }

            if (!gUser.Roles.Contains(customColorRole))
            {
                try
                {
                    _logger.LogDebug("Adding custom color role {role} to user {user} in guild {guild}", customColorRole.Log(), gUser.Log(), guild.Log());
                    await gUser.AddRoleAsync(customColorRole);
                    _logger.LogInformation("Added custom color role {role} to user {user} in guild {guild}", customColorRole.Log(), gUser.Log(), guild.Log());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed adding custom color role {role} to user {user} in guild {guild}", customColorRole.Log(), gUser.Log(), guild.Log());
                    return new Error<Exception>(ex);
                }
            }

            return new Success<IRole>(customColorRole);
        }

        /// <summary>
        /// Deletes a custom color role
        /// </summary>
        /// <param name="guild">Guild to delete role from</param>
        /// <param name="user">Owner of role</param>
        /// <returns>Success / None / Exception</returns>
        internal async Task<OneOf<Success, None, Error<Exception>>> DeleteCustomColorRole(IGuild guild, IUser user)
        {
            var roleName = DiscordUtils.GetCustomColorRoleName(user);
            var role = guild.Roles.FirstOrDefault(x => x.Name == roleName);

            if (role is null)
                return new None();

            try
            {
                _logger.LogDebug("Deleting custom color role {role} from {guild}", role.Log(), guild.Log());
                await role.DeleteAsync();
                _logger.LogInformation("Deleted custom color role {role} from {guild}", role.Log(), guild.Log());
                return new Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed deleting custom color role {role} from {guild}", role.Log(), guild.Log());
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
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigCustomColorAsync(IGuild guild, IRole? role)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);
            dbGuild.CustomColorPermissionRoleId = role?.Id ?? ulong.MinValue;

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
        #endregion
    }
}
