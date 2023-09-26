using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;

namespace SolarisBot.Discord.Commands
{
    [Group("roles", "Role related commands"), RequireContext(ContextType.Guild)]
    public sealed class RoleCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<RoleCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal RoleCommands(ILogger<RoleCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }
        protected override ILogger? GetLogger() => _logger;

        #region Admin
        [SlashCommand("view-all", "[MANAGE ROLES ONLY] View all roles and groups (Including empty ones)"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task ViewAllRolesAsync()
        {
            var roleGroups = await _dbContext.RoleGroups.Where(x => x.GId == Context.Guild.Id).ToArrayAsync();

            if (roleGroups.Length == 0)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            var strings = roleGroups.OrderBy(x => x.Identifier)
                .Select(x =>
                {
                    var title = $"{x.Identifier} ({(x.AllowOnlyOne ? "One of" : "Multi")}{(x.RequiredRoleId == 0 ? string.Empty : $", <@&{x.RequiredRoleId}> Only")})";
                    var rolesText = x.Roles.Any()
                        ? string.Join("\n", x.Roles.OrderBy(x => x.Identifier).Select(x => $"┗ {x.Identifier}(<@&{x.RId}>)"))
                        : "┗ (No roles assigned to group)";

                    return $"{title}\n{rolesText}";
                });

            await RespondEmbedAsync("List of Assignable Roles", string.Join("\n\n", strings));
        }

        [SlashCommand("group-create", "[MANAGE ROLES ONLY] Create role group (Identifiers only letters, numbers, and spaces)"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task CreateRoleGroupAsync([MinLength(2), MaxLength(20)] string identifier, [MinLength(2), MaxLength(200)] string description = "", bool oneof = true, IRole? requiredRole = null)
        {
            var identifierClean = identifier.Trim();
            var descriptionClean = description.Trim();
            if (!DiscordUtils.IsIdentifierValid(identifierClean) || descriptionClean.Length > 200)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput, isEphemeral: true);
                return;
            }

            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            var lowerName = identifierClean.ToLower();
            var roleGroup = guild.RoleGroups.FirstOrDefault(x => x.Identifier.ToLower() == lowerName)
                ?? new() { GId = Context.Guild.Id, Identifier = identifierClean };

            var logVerb = roleGroup.RgId == 0 ? "Creat" : "Updat";
            roleGroup.AllowOnlyOne = oneof;
            roleGroup.Description = descriptionClean;
            roleGroup.RequiredRoleId = requiredRole?.Id ?? 0;

            _dbContext.RoleGroups.Update(roleGroup);

            _logger.LogDebug("{intTag} {verb}ing role group {roleGroup} for guild {guild}", GetIntTag(), logVerb, roleGroup, Context.Guild.Log());
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                _logger.LogWarning("{intTag} Failed to {verb}e role group {roleGroup} for guild {guild}", GetIntTag(), logVerb.ToLower(), roleGroup, Context.Guild.Log());
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("{intTag} {verb}ed role group {roleGroup} for guild {guild}", GetIntTag(), logVerb, roleGroup, Context.Guild.Log());
            await RespondEmbedAsync($"Role Group {logVerb}ed", $"Identifier: **{roleGroup.Identifier}**\nOne Of: **{(roleGroup.AllowOnlyOne ? "Yes" : "No")}**\nDescription: **{(string.IsNullOrWhiteSpace(roleGroup.Description) ? "None" : roleGroup.Description)}**\nRequired: **{(roleGroup.RequiredRoleId == 0 ? "None" : $"<@&{roleGroup.RequiredRoleId}>")}**");
        }

        [SlashCommand("group-delete", "[MANAGE ROLES ONLY] Delete a role group"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task DeleteRoleGroupAsync(string identifier)
        {
            var identifierClean = identifier.Trim();
            if (!DiscordUtils.IsIdentifierValid(identifierClean))
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput, isEphemeral: true);
                return;
            }

            var lowerName = identifierClean.ToLower();
            var match = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GId == Context.Guild.Id && x.Identifier.ToLower() == lowerName);
            if (match == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            _dbContext.RoleGroups.Remove(match);

            _logger.LogDebug("{intTag} Deleting role group {roleGroup} from guild {guild}", GetIntTag(), match, Context.Guild.Log());
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                _logger.LogWarning("{intTag} Failed to delete role group {roleGroup} from guild {guild}", GetIntTag(), match, Context.Guild.Log());
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("{intTag} Deleted role group {roleGroup} from guild {guild}", GetIntTag(), match, Context.Guild.Log());
            await RespondEmbedAsync("Role Group Deleted", $"The role group with the identifier **\"{identifierClean}\"** has been deleted");
        }

        [SlashCommand("role-register", "[MANAGE ROLES ONLY] Register role group (Identifiers only letters, numbers, and spaces)"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task RegisterRoleAsync(IRole role, [MinLength(2), MaxLength(20)] string identifier, string group, string description = "")
        {
            var descriptionClean = description.Trim();
            var identifierNameClean = identifier.Trim();
            var groupNameCleanLower = group.Trim().ToLower();

            if (!DiscordUtils.IsIdentifierValid(identifierNameClean) || !DiscordUtils.IsIdentifierValid(groupNameCleanLower) || descriptionClean.Length > 200)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput, isEphemeral: true);
                return;
            }

            if (await _dbContext.Roles.FirstOrDefaultAsync(x => x.RId == role.Id) != null)
            {
                await RespondErrorEmbedAsync("Already Registered", "Role is already registered");
                return;
            }

            var roleGroup = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GId == Context.Guild.Id && x.Identifier.ToLower() == groupNameCleanLower);
            if (roleGroup == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            var lowerIdentifierName = identifierNameClean.ToLower();
            if (roleGroup.Roles.FirstOrDefault(x => x.Identifier.ToLower() == lowerIdentifierName) != null)
            {
                await RespondErrorEmbedAsync("Already Registered", "A Role with that identifier is already registered");
                return;
            }

            var dbRole = new DbRole()
            {
                Identifier = identifierNameClean,
                RId = role.Id,
                RgId = roleGroup.RgId,
                Description = descriptionClean
            };

            _dbContext.Roles.Add(dbRole);

            _logger.LogDebug("{intTag} Registering role {role} to group {roleGroup} in guild {guild}", GetIntTag(), dbRole, roleGroup, Context.Guild.Log());
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                _logger.LogWarning("{intTag} Failed to register role {role} to group {roleGroup} in guild {guild}", GetIntTag(), dbRole, roleGroup, Context.Guild.Log());
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("{intTag} Registered role {role} to group {roleGroup} in guild {guild}", GetIntTag(), dbRole, roleGroup, Context.Guild.Log());
            await RespondEmbedAsync("Role Registered", $"Group: **{roleGroup.Identifier}**\nRole: **{role.Mention}**\nIdentifier: **{dbRole.Identifier}**\nDescription: **{(string.IsNullOrWhiteSpace(dbRole.Description) ? "None" : dbRole.Description)}**");
        }

        [SlashCommand("role-unregister", "[MANAGE ROLES ONLY] Unregister a role"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task UnregisterRoleAsync(string identifier)
        {
            var identifierClean = identifier.Trim().ToLower();

            if (!DiscordUtils.IsIdentifierValid(identifierClean))
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput, isEphemeral: true);
                return;
            }

            var role = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Identifier.ToLower() == identifierClean);
            if (role == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            _dbContext.Roles.Remove(role);

            _logger.LogDebug("{intTag} Unregistering role {role} from groups", GetIntTag(), role);
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                _logger.LogWarning("{intTag} Failed to unregister role {role} from groups", GetIntTag(), role);
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("{intTag} Unregistered role {role} from groups", GetIntTag(), role);
            await RespondEmbedAsync("Role Unegistered", $"A role with the identifier **\"{identifierClean}\"** has been unregistered");
        }
        #endregion

        #region User
        [SlashCommand("view", "View all roles and groups")]
        public async Task ViewRolesAsync()
        {
            var roleGroups = await _dbContext.RoleGroups.Where(x => x.GId == Context.Guild.Id).ToArrayAsync();

            if (roleGroups.Length == 0)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            var groupFields = new List<EmbedFieldBuilder>();
            foreach (var roleGroup in roleGroups.OrderBy(x => x.Identifier))
            {
                var roles = roleGroup.Roles;
                if (!roles.Any()) continue;

                var roleList = string.Join(", ", roles.OrderBy(x => x.Identifier).Select(x => $"{x.Identifier}(<@&{x.RId}>)"));
                var fieldBuilder = new EmbedFieldBuilder()
                {
                    IsInline = true,
                    Name = $"{roleGroup.Identifier} ({(roleGroup.AllowOnlyOne ? "One of" : "Multi")})",
                    Value = $"{(roleGroup.RequiredRoleId == 0 ? string.Empty : $"*<@&{roleGroup.RequiredRoleId}> only*\n")}{(string.IsNullOrWhiteSpace(roleGroup.Description) ? string.Empty : roleGroup.Description + "\n")}Roles: {roleList}"
                };
                groupFields.Add(fieldBuilder);
            }

            if (!groupFields.Any())
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            var embedBuilder = DiscordUtils.EmbedBuilder("Self-Assignable Roles")
                .WithFields(groupFields);

            await RespondEmbedAsync(embedBuilder.Build(), isEphemeral: true);
        }

        [SlashCommand("select", "Select roles from a group"), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task SelectRolesAsync([MinLength(2), MaxLength(20)] string groupname)
        {
            if (Context.User is not SocketGuildUser gUser)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden, isEphemeral: true);
                return;
            }

            var cleanGroupName = groupname.Trim().ToLower();
            if (!DiscordUtils.IsIdentifierValid(cleanGroupName))
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput, isEphemeral: true);
                return;
            }

            var group = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GId == Context.Guild.Id && x.Identifier.ToLower() == cleanGroupName);
            var roles = group?.Roles;
            if (roles == null || !roles.Any())
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            if (group!.RequiredRoleId != 0 && !gUser.Roles.Select(x => x.Id).Contains(group.RequiredRoleId))
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden, isEphemeral: true);
                return;
            }

            var menuBuilder = new SelectMenuBuilder()
            {
                CustomId = $"roleselector.{group.RgId}",
                Placeholder = group.AllowOnlyOne ? "Select a role..." : "Select roles...",
                MaxValues = group.AllowOnlyOne ? 1 : roles.Count,
                Type = ComponentType.SelectMenu
            };

            foreach (var role in roles)
            {
                var desc = role.Description;
                if (string.IsNullOrWhiteSpace(desc))
                    desc = role.Identifier;
                menuBuilder.AddOption(role.Identifier, role.Identifier, desc);
            }

            var compBuilder = new ComponentBuilder()
                .WithSelectMenu(menuBuilder);
            try
            {
                await RespondAsync($"Roles in group {group.Identifier}:", components: compBuilder.Build(), ephemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{intTag} Failed to respond to interaction", GetIntTag());
                await RespondErrorEmbedAsync(ex);
            }
        }

        [ComponentInteraction("roleselector.*", true), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task SelectRoleResponseAsync(string rgid, string[] selections)
        {
            if (Context.User is not SocketGuildUser gUser)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden, isEphemeral: true);
                return;
            }

            if (selections.Length == 0 || !ulong.TryParse(rgid, out var parsedGid))
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput, isEphemeral: true);
                return;
            }

            var roleGroup = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.RgId == parsedGid && x.GId == gUser.Guild.Id);
            if (roleGroup == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            var dbRoles = roleGroup.Roles;
            var userRoleIds = gUser.Roles.Select(x => x.Id);
            var rolesToAdd = new List<DbRole>();
            var rolesToRemove = new List<DbRole>();
            var rolesInvalid = new List<string>();

            if (roleGroup.AllowOnlyOne)
            {
                var dbRole = dbRoles.FirstOrDefault(x => x.Identifier == selections[0]);
                if (dbRole != null)
                {
                    var alreadyPossesedRoles = dbRoles.Where(x => userRoleIds.Contains(x.RId));
                    rolesToRemove.AddRange(alreadyPossesedRoles);
                    if (!rolesToRemove.Contains(dbRole))
                        rolesToAdd.Add(dbRole);
                }
                else
                    rolesInvalid.Add(selections[0]);
            }
            else
            {
                foreach (var selection in selections)
                {
                    var dbRole = dbRoles.FirstOrDefault(x => x.Identifier == selection);
                    if (dbRole == null)
                    {
                        rolesInvalid.Add(selection);
                        continue;
                    }

                    if (userRoleIds.Contains(dbRole.RId))
                        rolesToRemove.Add(dbRole);
                    else
                        rolesToAdd.Add(dbRole);
                }
            }

            try
            {
                var groupFields = new List<EmbedFieldBuilder>();

                if (rolesToAdd.Any())
                {
                    var rolesToAddText = string.Join(", ", rolesToAdd.Select(x => $"{x.Identifier}(<@&{x.RId}>)"));
                    _logger.LogDebug("{intTag} Adding roles {addedRoles} to user {userData} in guild {guild}", GetIntTag(), rolesToAddText, gUser.Log(), Context.Guild.Log());
                    await gUser.AddRolesAsync(rolesToAdd.Select(x => x.RId));
                    groupFields.Add(new EmbedFieldBuilder()
                    {
                        IsInline = true,
                        Name = "Roles Added",
                        Value = rolesToAddText
                    });
                    _logger.LogInformation("{intTag} Added roles {addedRoles} to user {userData} in guild {guild}", GetIntTag(), rolesToAddText, gUser.Log(), Context.Guild.Log());
                }
                if (rolesToRemove.Any())
                {
                    var rolesToRemoveText = string.Join(", ", rolesToRemove.Select(x => $"{x.Identifier}(<@&{x.RId}>)"));
                    _logger.LogDebug("{intTag} Removing roles {removedRoles} from user {userData} in guild {guild}", GetIntTag(), rolesToRemoveText, gUser.Log(), Context.Guild.Log());
                    await gUser.RemoveRolesAsync(rolesToRemove.Select(x => x.RId));
                    groupFields.Add(new EmbedFieldBuilder()
                    {
                        IsInline = true,
                        Name = "Roles Removed",
                        Value = rolesToRemoveText
                    });
                    _logger.LogInformation("{intTag} Removed roles {removedRoles} from user {userData} in guild {guild}", GetIntTag(), rolesToRemoveText, gUser.Log(), Context.Guild.Log());
                }
                if (rolesInvalid.Any())
                {
                    var rolesInvalidText = string.Join(", ", rolesInvalid);
                    groupFields.Add(new EmbedFieldBuilder()
                    {
                        IsInline = true,
                        Name = "Invalid Roles",
                        Value = rolesInvalidText
                    });
                    _logger.LogWarning("{intTag} Failed to find roles {invalidRoles} in group {roleGroup}, could not apply to user {userData}", GetIntTag(), rolesInvalidText, roleGroup, gUser.Log());
                }

                if (!groupFields.Any())
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                    return;
                }

                var embedBuilder = DiscordUtils.EmbedBuilder("Roles Updated")
                    .WithFields(groupFields);

                await RespondEmbedAsync(embedBuilder.Build(), isEphemeral: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{intTag} Failed responding to interaction", GetIntTag());
                await RespondErrorEmbedAsync(ex);
            }
        }
        #endregion
    }
}
