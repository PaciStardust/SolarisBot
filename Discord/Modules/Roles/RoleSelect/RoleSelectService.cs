using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles.RoleSelect
{
    [Module("roles/roleselect"), AutoLoadService]
    internal class RoleSelectService //todo: [FEATURE] Reaction Roles
    {
        private readonly ILogger<RoleSelectService> _logger;
        private readonly DatabaseService _dbService;
        public RoleSelectService(ILogger<RoleSelectService> logger, DatabaseService dbService)
        {
            _dbService = dbService;
            _logger = logger;
        }

        #region Commands
        /// <summary>
        /// Selects a role group for a user in a guild and checks for permission
        /// </summary>
        /// <param name="user">User to get group for</param>
        /// <param name="identifier">Identifier for search</param>
        /// <returns>Role group on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbRoleGroup>, Error<string>, Error<Exception>>> SelectRoleGroupAsync(IUser user, string identifier)
        {
            if (user is not SocketGuildUser gUser)
                return new Error<string>(StandardError.FailedConversion("executing user", "SocketGuildUser"));

            var roleGroupMatch = await GetRoleGroupForIdentifierAsync(gUser.Guild.Id, identifier.Trim());

            if (roleGroupMatch is null || roleGroupMatch.RoleConfigs.Count == 0)
                return new Error<string>(StandardError.NoResults);

            if (roleGroupMatch!.RequiredRoleId != ulong.MinValue && !gUser.Roles.Select(x => x.Id).Contains(roleGroupMatch.RequiredRoleId))
                return new Error<string>(StandardError.RoleRequired(roleGroupMatch.RequiredRoleId));

            return new Success<DbRoleGroup>(roleGroupMatch);
        }

        /// <summary>
        /// Handles the role selection event
        /// </summary>
        /// <param name="user">User executing the event</param>
        /// <param name="rgid">Role group ID</param>
        /// <param name="selections">Selections made by user</param>
        /// <returns>A result embed on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<Embed>, Error<string>, Error<Exception>>> HandleRoleSelectorInteractionAsync(IUser user, string rgid, string[] selections)
        {
            if (user is not SocketGuildUser gUser)
                return new Error<string>(StandardError.FailedConversion("executing user", "SocketGuildUser"));

            if (selections.Length == 0)
                return new Error<string>("No selections have been made");

            if (!ulong.TryParse(rgid, out var parsedGid) || parsedGid == 0)
                return new Error<string>($"Could not parse RGId **{rgid}**");

            using var dbCtx = _dbService.GetContext();
            var roleGroup = await dbCtx.RoleGroups.ForGuildWithRoles(gUser.Guild.Id).FirstOrDefaultAsync(x => x.RoleGroupId == parsedGid);
            if (roleGroup is null)
                return new Error<string>(StandardError.NoResults);

            if (roleGroup.RequiredRoleId != ulong.MinValue && gUser.FindRole(roleGroup.RequiredRoleId) is null)
                return new Error<string>(StandardError.RoleRequired(roleGroup.RequiredRoleId));

            var dbRoles = roleGroup.RoleConfigs;
            var invalidRoles = new List<string>();
            var selectedRoles = new List<DbRoleConfig>();
            if (roleGroup.AllowOnlyOne)
                selections = selections[0..1]; //remove all but first
            foreach (var selection in selections)
            {
                var match = dbRoles.FirstOrDefault(x => x.Identifier == selection);
                if (match is null)
                    invalidRoles.Add(selection);
                else
                    selectedRoles.Add(match);
            }

            return await AssignRolesToUser(gUser, selectedRoles, invalidRoles);
        }

        /// <summary>
        /// Assigns roles to user
        /// </summary>
        /// <param name="user">User to assign roles to</param>
        /// <param name="roleConfigs">RoleConfigs to assing</param>
        /// <param name="rolesInvalid">Invalid supplied roles</param>
        /// <returns>An embed summarizing the result on success / Error string / Exception</returns>
        private async Task<OneOf<Success<Embed>, Error<string>, Error<Exception>>> AssignRolesToUser(IUser user, IEnumerable<DbRoleConfig>? roleConfigs = null, IEnumerable<string>? rolesInvalid = null)
        {
            if (user is not SocketGuildUser gUser)
                return new Error<string>(StandardError.FailedConversion("executing user", "SocketGuildUser"));

            var groupFields = new List<EmbedFieldBuilder>();

            if (roleConfigs?.Any() ?? false)
            {
                var userRoleIds = gUser.Roles.Select(x => x.Id);
                var rolesToAdd = new List<DbRoleConfig>();
                var rolesToRemove = new List<DbRoleConfig>();
                var rolesMissing = new List<DbRoleConfig>();

                foreach (var roleConfig in roleConfigs)
                {
                    if (!gUser.Guild.Roles.Any(x => x.Id == roleConfig.RoleId))
                        rolesMissing.Add(roleConfig);
                    else if (userRoleIds.Contains(roleConfig.RoleId))
                        rolesToRemove.Add(roleConfig);
                    else
                        rolesToAdd.Add(roleConfig);
                }

                if (rolesToAdd.Count != 0)
                {
                    var rolesToAddText = GenerateRoleList(rolesToAdd);
                    try
                    {
                        _logger.LogDebug("Adding roles {addedRoles} to user {userData} in guild {guild}", rolesToAddText, gUser.Log(), gUser.Guild.Log());
                        await gUser.AddRolesAsync(rolesToAdd.Select(x => x.RoleId));
                        groupFields.Add(new EmbedFieldBuilder()
                        {
                            IsInline = true,
                            Name = "Roles Added",
                            Value = rolesToAddText
                        });
                        _logger.LogInformation("Added roles {addedRoles} to user {userData} in guild {guild}", rolesToAddText, gUser.Log(), gUser.Guild.Log());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed adding roles {addedRoles} to user {userData} in guild {guild}", rolesToAddText, gUser.Log(), gUser.Guild.Log());
                        return new Error<Exception>(ex);
                    }
                }

                if (rolesToRemove.Count != 0)
                {
                    var rolesToRemoveText = GenerateRoleList(rolesToRemove);
                    try
                    {
                        _logger.LogDebug("Removing roles {removedRoles} from user {userData} in guild {guild}", rolesToRemoveText, gUser.Log(), gUser.Guild.Log());
                        await gUser.RemoveRolesAsync(rolesToRemove.Select(x => x.RoleId));
                        groupFields.Add(new EmbedFieldBuilder()
                        {
                            IsInline = true,
                            Name = "Roles Removed",
                            Value = rolesToRemoveText
                        });
                        _logger.LogInformation("Removed roles {removedRoles} from user {userData} in guild {guild}", rolesToRemoveText, gUser.Log(), gUser.Guild.Log());
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed removing roles {removedRoles} from user {userData} in guild {guild}", rolesToRemoveText, gUser.Log(), gUser.Guild.Log());
                        return new Error<Exception>(ex);
                    }
                }

                if (rolesMissing.Count != 0)
                {
                    var rolesMissingText = GenerateRoleList(rolesMissing);
                    groupFields.Add(new EmbedFieldBuilder()
                    {
                        IsInline = true,
                        Name = "Missing Roles",
                        Value = rolesMissingText
                    });
                    _logger.LogDebug("Failed to find roles {missingRoles} guild role list, could not apply to user {userData}", rolesMissingText, gUser.Log());
                }
            }

            if (rolesInvalid?.Any() ?? false)
            {
                var rolesInvalidText = string.Join(", ", rolesInvalid);
                groupFields.Add(new EmbedFieldBuilder()
                {
                    IsInline = true,
                    Name = "Invalid Roles",
                    Value = rolesInvalidText
                });
                _logger.LogDebug("Failed to find roles {invalidRoles} DB role list, could not apply to user {userData}", rolesInvalidText, gUser.Log());
            }

            if (groupFields.Count == 0)
                return new Error<string>(StandardError.NoResults);

            var embedBuilder = EmbedFactory.Builder()
                .WithTitle("Roles Updated")
                .WithFields(groupFields);

            return new Success<Embed>(embedBuilder.Build());
        }

        private static string GenerateRoleList(IEnumerable<DbRoleConfig> roleConfigs)
            => string.Join(", ", roleConfigs.Select(x => $"{x.Identifier}(<@&{x.RoleId}>)"));
        #endregion

        #region Commands - Config
        /// <summary>
        /// Creates or updates a role group in a guild
        /// </summary>
        /// <param name="guild">Guild to create or update in</param>
        /// <param name="identifier">Identifier of group</param>
        /// <param name="description">Description of group</param>
        /// <param name="oneOf">Should only allow one role in group to be picked?</param>
        /// <param name="requiredRole">Required role to pick from group</param>
        /// <returns>Is new + role group on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<(bool,DbRoleGroup)>, Error<string>, Error<Exception>>> CreateRoleGroupAsync(IGuild guild, string identifier, string description, bool oneOf, IRole? requiredRole)
        {
            var identifierTrimmed = identifier.Trim();
            var descriptionTrimmed = description.Trim();
            if (!DiscordUtils.IsIdentifierValid(identifierTrimmed))
                return new Error<string>(StandardError.InvalidIdentifier(identifierTrimmed));
            if (descriptionTrimmed.Length > 200)
                return new Error<string>("Descriptions must be 200 characters or shorter");

            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id, x => x.Include(y => y.RoleGroups));

            var roleGroup = dbGuild.RoleGroups.FirstOrDefault(x => x.Identifier.Equals(identifierTrimmed, StringComparison.OrdinalIgnoreCase))
                ?? new() { GuildId = guild.Id, Identifier = identifierTrimmed };

            var isNew = roleGroup.RoleGroupId == ulong.MinValue;
            roleGroup.AllowOnlyOne = oneOf;
            roleGroup.Description = descriptionTrimmed;
            roleGroup.RequiredRoleId = requiredRole?.Id ?? 0;

            dbCtx.RoleGroups.Update(roleGroup);

            var logVerb = isNew ? "Creat" : "Updat";
            _logger.LogDebug("{verb}ing role group {roleGroup} for guild {guild}", logVerb, roleGroup, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed {verb}ing role group {roleGroup} for guild {guild}", logVerb.ToLower(), roleGroup, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("{verb}ed role group {roleGroup} for guild {guild}", logVerb, roleGroup, guild.Log());
            return new Success<(bool, DbRoleGroup)>((isNew, roleGroup));
        }

        /// <summary>
        /// Deletes a role group from a guild
        /// </summary>
        /// <param name="guild">Guild to delete from</param>
        /// <param name="identifier">Identifier of group</param>
        /// <returns>Deleted role group on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbRoleGroup>, Error<string>, Error<Exception>>> DeleteRoleGroupAsync(IGuild guild, string identifier)
        {
            using var dbCtx = _dbService.GetContext();
            var identifierSearch = identifier.Trim().ToLower();
            var match = await dbCtx.RoleGroups.ForGuild(guild.Id).FirstOrDefaultAsync(x => x.Identifier.ToLower() == identifierSearch); //No ordinal because EF
            if (match is null)
                return new Error<string>(StandardError.NoResults);

            dbCtx.RoleGroups.Remove(match);

            _logger.LogDebug("Deleting role group {roleGroup} from guild {guild}", match, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed deleting role group {roleGroup} from guild {guild}", match, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Deleted role group {roleGroup} from guild {guild}", match, guild.Log());
            return new Success<DbRoleGroup>(match);
        }

        /// <summary>
        /// Registers a role to a role group in a guild
        /// </summary>
        /// <param name="guild">Guild to register in</param>
        /// <param name="role">Role to register</param>
        /// <param name="group">Group to register to</param>
        /// <param name="identifier">Identifier of role</param>
        /// <param name="description">Description</param>
        /// <returns>Created role config on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbRoleConfig>, Error<string>, Error<Exception>>> RegisterRoleAsync(IGuild guild, IRole role, string group, string identifier, string description)
        {
            if (string.IsNullOrWhiteSpace(identifier))
                identifier = role.Name;

            var descriptionTrimmed = description.Trim();
            var identifierTrimmed = identifier.Trim();
            var groupSearch = group.Trim().ToLower();

            var identifierValid = DiscordUtils.IsIdentifierValid(identifierTrimmed);
            if (!identifierValid || !DiscordUtils.IsIdentifierValid(groupSearch))
                return new Error<string>(StandardError.InvalidIdentifier(identifierValid ? groupSearch : identifierTrimmed));
            if (descriptionTrimmed.Length > 200)
                return new Error<string>("Descriptions must be 200 characters or shorter");

            using var dbCtx = _dbService.GetContext();
            if (await dbCtx.RoleConfigs.FirstOrDefaultAsync(x => x.RoleId == role.Id) is not null)
                return new Error<string>("Role is already registered");

            var roleGroup = await dbCtx.RoleGroups.ForGuildWithRoles(guild.Id).FirstOrDefaultAsync(x => x.Identifier.ToLower() == groupSearch); //No ordinal because EF
            if (roleGroup is null)
                return new Error<string>(StandardError.NoResults);

            if (roleGroup.RoleConfigs.FirstOrDefault(x => x.Identifier.Equals(identifierTrimmed, StringComparison.OrdinalIgnoreCase)) is not null)
                return new Error<string>("A Role with that identifier is already registered");

            var dbRole = new DbRoleConfig()
            {
                Identifier = identifierTrimmed,
                RoleId = role.Id,
                RoleGroupId = roleGroup.RoleGroupId,
                Description = descriptionTrimmed
            };

            dbCtx.RoleConfigs.Add(dbRole);

            _logger.LogDebug("Registering role {role} to group {roleGroup} in guild {guild}", dbRole, roleGroup, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed registering role {role} to group {roleGroup} in guild {guild}", dbRole, roleGroup, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Registered role {role} to group {roleGroup} in guild {guild}", dbRole, roleGroup, guild.Log());
            return new Success<DbRoleConfig>(dbRole);
        }

        /// <summary>
        /// Unregisters a role from a role group in a guild
        /// </summary>
        /// <param name="guild">Guild to unregister in</param>
        /// <param name="group">Group to unregister from</param>
        /// <param name="identifier">Identifier of role</param>
        /// <returns>Deleted role config on success / Exception</returns>
        internal async Task<OneOf<Success<DbRoleConfig>, Error<string>, Error<Exception>>> UnregisterRoleAsync(IGuild guild, string group, string identifier)
        {
            var groupSearch = group.Trim().ToLower();
            var identifierSearch = identifier.Trim();

            using var dbCtx = _dbService.GetContext();
            var dbGroup = await dbCtx.RoleGroups.ForGuildWithRoles(guild.Id).FirstOrDefaultAsync(x => x.Identifier.ToLower() == groupSearch); //No ordinal because EF
            var dbRole = dbGroup?.RoleConfigs.FirstOrDefault(x => x.Identifier.Equals(identifierSearch, StringComparison.OrdinalIgnoreCase));
            if (dbRole is null)
                return new Error<string>(StandardError.NoResults);

            dbCtx.RoleConfigs.Remove(dbRole);

            _logger.LogDebug("Unregistering role {role} from group {group} in guild {guild}", dbRole, dbGroup, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed unregistering role {role} from group {group} in guild {guild}", dbRole, dbGroup, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Unregistered role {role} from group {group} in guild {guild}", dbRole, dbGroup, guild.Log());
            return new Success<DbRoleConfig>(dbRole);
        }
        #endregion

        #region Utility
        /// <summary>
        /// Generates a selector for a role group
        /// </summary>
        /// <param name="roleGroup">Role group to make selector for</param>
        /// <returns>Generated component</returns>
        internal static MessageComponent GenerateRoleGroupSelector(DbRoleGroup roleGroup)
        {
            var roles = roleGroup.RoleConfigs;

            var menuBuilder = new SelectMenuBuilder()
            {
                CustomId = $"solaris_roleselector.{roleGroup.RoleGroupId}",
                Placeholder = roleGroup.AllowOnlyOne ? "Select a role..." : "Select roles...",
                MaxValues = roleGroup.AllowOnlyOne ? 1 : roles.Count,
                Type = ComponentType.SelectMenu
            };

            foreach (var role in roles)
            {
                var desc = role.Description;
                if (string.IsNullOrWhiteSpace(desc))
                    desc = role.Identifier;
                menuBuilder.AddOption(role.Identifier, role.Identifier, desc);
            }

            return new ComponentBuilder()
                .WithSelectMenu(menuBuilder)
                .Build();
        }

        /// <summary>
        /// Tries finding a matching role group for an identifier in a guild
        /// </summary>
        /// <param name="guildId">Id of guild to search</param>
        /// <param name="identifierSearch">Identifier to search</param>
        /// <returns>Match or null</returns>
        internal async Task<DbRoleGroup?> GetRoleGroupForIdentifierAsync(ulong guildId, string identifierSearch)
        {
            var roleGroups = await GetRoleGroupsForGuildAsync(guildId);

            var result = roleGroups.FirstOrDefault(x => x.Identifier.Equals(identifierSearch, StringComparison.OrdinalIgnoreCase))
                ?? roleGroups.FirstOrDefault(x => x.RoleConfigs.Any(y => y.Identifier.Equals(identifierSearch, StringComparison.OrdinalIgnoreCase)))
                ?? roleGroups.FirstOrDefault(x => x.Identifier.StartsWith(identifierSearch, StringComparison.OrdinalIgnoreCase))
                ?? roleGroups.FirstOrDefault(x => x.RoleConfigs.Any(y => y.Identifier.StartsWith(identifierSearch, StringComparison.OrdinalIgnoreCase)))
                ?? roleGroups.FirstOrDefault(x => x.Identifier.Contains(identifierSearch, StringComparison.OrdinalIgnoreCase))
                ?? roleGroups.FirstOrDefault(x => x.RoleConfigs.Any(y => y.Identifier.Contains(identifierSearch, StringComparison.OrdinalIgnoreCase)));

            return result;
        }

        /// <summary>
        /// Gets role groups for a guild
        /// </summary>
        /// <param name="guild">Id of guild to get role groups from</param>
        /// <returns>Role groups of guild</returns>
        internal async Task<DbRoleGroup[]> GetRoleGroupsForGuildAsync(ulong guildId)
        {
            using var dbCtx = _dbService.GetContext();
            var roleGroups = await dbCtx.RoleGroups.ForGuildWithRoles(guildId).ToArrayAsync();
            return roleGroups;
        }
        #endregion
    }
}
