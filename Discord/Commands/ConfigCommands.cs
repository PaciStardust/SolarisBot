using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Commands
{
    [Group("config", "[ADMIN ONLY] Configure Solaris"), RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.Administrator)]
    public sealed class ConfigCommands : SolarisInteractionModuleBase
    {
        [Group("roles", "Role Configuration")]
        public sealed class ConfigRoleCommands : SolarisInteractionModuleBase
        {
            private readonly ILogger<ConfigRoleCommands> _logger;
            private readonly DatabaseContext _dbContext;
            internal ConfigRoleCommands(ILogger<ConfigRoleCommands> logger, DatabaseContext dbctx)
            {
                _dbContext = dbctx;
                _logger = logger;
            }
            protected override ILogger? GetLogger() => _logger;

            [SlashCommand("list", "List all roles and groups")]
            public async Task ListRolesAsync()
            {
                var roleGroups = _dbContext.RoleGroups.Where(x => x.GId == Context.Guild.Id);

                if (!roleGroups.Any())
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                //todo: [FEATURE] Remove special on other removal?

                var strings = (await roleGroups.ToArrayAsync()).OrderBy(x => x.Name)
                    .Select(x =>
                    {
                        var title = $"{x.Name} ({(x.AllowOnlyOne ? "One of" : "Multi")}{(x.RequiredRoleId == 0 ? string.Empty : $", <@&{x.RequiredRoleId}> Only")})";
                        var rolesText = x.Roles.Any()
                            ? string.Join("\n", x.Roles.OrderBy(x => x.Name).Select(x => $"┗ {x.Name}(<@&{x.RId}>)"))
                            : "┗ (No roles assigned to group)";

                        return $"{title}\n{rolesText}";
                    });

                await RespondEmbedAsync("List of Assignable Roles", string.Join("\n\n", strings));
            }

            [SlashCommand("create-group", "Create a role group (Group names can only be made of 2-20 letters, numbers, and spaces)")] //todo: override with same name
            public async Task CreateRoleGroupAsync([MinLength(2), MaxLength(20)] string name, [MinLength(2), MaxLength(200)] string description = "", bool allowMultiple = false, IRole? requiredRole = null)
            {
                var nameClean = name.Trim();
                var descriptionClean = description.Trim();
                if (!IsIdentifierValid(nameClean) || descriptionClean.Length > 200)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput);
                    return;
                }

                var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

                var lowerName = nameClean.ToLower();
                if (guild.RoleGroups.FirstOrDefault(x => x.Name.ToLower() == lowerName) != null)
                {
                    await RespondErrorEmbedAsync("Already Exists", $"A group by the name of \"{nameClean}\" already exists");
                    return;
                }

                var roleGroup = new DbRoleGroup()
                {
                    AllowOnlyOne = allowMultiple,
                    GId = Context.Guild.Id,
                    Name = nameClean,
                    Description = descriptionClean,
                    RequiredRoleId = requiredRole?.Id ?? 0
                };

                await _dbContext.RoleGroups.AddAsync(roleGroup);

                if (await _dbContext.TrySaveChangesAsync() == -1)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                else
                {
                    _logger.LogInformation("Created role group {groupName} for guild {guildId}", roleGroup.Name, roleGroup.GId);
                    await RespondEmbedAsync("Role Group Created", $"A role group with the name \"{nameClean}\" has been created"); //todo: better summary
                }
            }

            [SlashCommand("delete-group", "Delete a role group")]
            public async Task DeleteRoleGroupAsync(string name)
            {
                var nameClean = name.Trim();
                if (!IsIdentifierValid(nameClean))
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput);
                    return;
                }

                var lowerName = nameClean.ToLower();
                var match = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GId == Context.Guild.Id && x.Name.ToLower() == lowerName);
                if (match == null)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                _dbContext.RoleGroups.Remove(match);

                if (await _dbContext.TrySaveChangesAsync() == -1)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                else
                {
                    _logger.LogInformation("Deleted role group {groupName} from guild {guildId}", match.Name, match.GId);
                    await RespondEmbedAsync("Role Group Deleted", $"A role group with the name \"{nameClean}\" has been deleted");
                }
            }

            [SlashCommand("register-role", "Register a role to a group (Identifier names can only be made of 2-20 letters, numbers, and spaces)")] //todo: override with same name
            public async Task RegisterRoleAsync(IRole role, [MinLength(2), MaxLength(20)] string identifier, string group,  string description = "")
            {
                var descriptionClean = description.Trim();
                var identifierNameClean = identifier.Trim();
                var groupNameCleanLower = group.Trim().ToLower();

                if (!IsIdentifierValid(identifierNameClean) || !IsIdentifierValid(groupNameCleanLower) || descriptionClean.Length > 200)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput);
                    return;
                }

                if (await _dbContext.Roles.FirstOrDefaultAsync(x => x.RId == role.Id) != null)
                {
                    await RespondErrorEmbedAsync("Already Registered", "Role is already registered");
                    return;
                }

                var roleGroup = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GId == Context.Guild.Id && x.Name.ToLower() == groupNameCleanLower);
                if (roleGroup == null)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                var lowerIdentifierName = identifierNameClean.ToLower(); 
                if (roleGroup.Roles.FirstOrDefault(x => x.Name.ToLower() == lowerIdentifierName) != null)
                {
                    await RespondErrorEmbedAsync("Already Registered", "A Role by that identifier is already registered");
                    return;
                }

                var dbRole = new DbRole()
                {
                    Name = identifierNameClean,
                    RId = role.Id,
                    RgId = roleGroup.RgId,
                    Description = descriptionClean
                };

                await _dbContext.Roles.AddAsync(dbRole);

                if (await _dbContext.TrySaveChangesAsync() == -1)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                else
                {
                    _logger.LogInformation("Role with identifier {roleName} registered to group {groupName} in guild {guildId}", dbRole.Name, roleGroup.Name, roleGroup.GId);
                    await RespondEmbedAsync("Role Registered", $"A role with the identifier \"{identifierNameClean}\" has been registered"); //todo: better summary
                }
            }

            [SlashCommand("unregister-role", "Unregister a role")]
            public async Task UnregisterRoleAsync(string identifier)
            {
                var identifierNameClean = identifier.Trim().ToLower();

                if (!IsIdentifierValid(identifierNameClean))
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput);
                    return;
                }

                var role = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Name.ToLower() == identifierNameClean);
                if (role == null)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                _dbContext.Roles.Remove(role);

                if (await _dbContext.TrySaveChangesAsync() == -1)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                else
                {
                    _logger.LogInformation("Role with identifier {roleName} unregistered from groups", role.Name);
                    await RespondEmbedAsync("Role Unegistered", $"A role with the identifier \"{identifierNameClean}\" has been unregistered");
                }
            }

            [SlashCommand("test-view-roles", "View all role groups")]
            public async Task TestViewRolesAsync() //todo: TEST
            {
                var roleGroups = _dbContext.RoleGroups.Where(x => x.GId == Context.Guild.Id);

                if (!roleGroups.Any())
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                var groupFields = new List<EmbedFieldBuilder>();
                foreach (var roleGroup in roleGroups.OrderBy(x => x.Name))
                {
                    var roles = roleGroup.Roles;
                    if (!roles.Any()) continue;

                    var roleList = string.Join(", ", roles.OrderBy(x => x.Name).Select(x => $"{x.Name}(<@&{x.RId}>)"));
                    var fieldBuilder = new EmbedFieldBuilder()
                    {
                        IsInline = true,
                        Name = $"{roleGroup.Name} ({(roleGroup.AllowOnlyOne ? "One of" : "Multi")})",
                        Value = $"{(roleGroup.RequiredRoleId == 0 ? string.Empty : $"<@&{roleGroup.RequiredRoleId}> Only\n")}{(string.IsNullOrWhiteSpace(roleGroup.Description) ? string.Empty : roleGroup.Description + "\n")}Roles: {roleList}"
                    };
                    groupFields.Add(fieldBuilder);
                }

                if (!groupFields.Any())
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                var embedBuilder = new EmbedBuilder()
                {
                    Fields = groupFields,
                    Color = Color.Blue,
                    Title = "Self-Assignable Roles"
                };

                await RespondEmbedAsync(embedBuilder.Build());
            }

            [SlashCommand("test-select-roles", "Select roles")]
            public async Task TestSelectRolesAsync([MinLength(2), MaxLength(20)] string groupname)
            { 
                if (Context.User is not SocketGuildUser gUser)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden);
                    return;
                }

                var cleanGroupName = groupname.Trim().ToLower();
                if (!IsIdentifierValid(cleanGroupName))
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput);
                    return;
                }

                var group = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GId == Context.Guild.Id && x.Name.ToLower() == cleanGroupName);
                var roles = group?.Roles;
                if (roles == null || !roles.Any())
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                if (group!.RequiredRoleId != 0 && !gUser.Roles.Select(x => x.Id).Contains(group.RequiredRoleId))
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden);
                    return;
                }

                var menuBuilder = new SelectMenuBuilder()
                {
                    CustomId = $"testroleselector.{group.RgId}",
                    Placeholder = "Select Roles...",
                    MaxValues = group.AllowOnlyOne ? 1 : roles.Count,
                    Type = ComponentType.SelectMenu
                };

                foreach (var role in roles)
                {
                    var desc = role.Description;
                    if (string.IsNullOrWhiteSpace(desc))
                        desc = role.Name;
                    menuBuilder.AddOption(role.Name, role.Name, desc);
                }

                var compBuilder = new ComponentBuilder()
                    .WithSelectMenu(menuBuilder);
                try
                {
                    await RespondAsync(components: compBuilder.Build());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to respond to interaction");
                    await RespondErrorEmbedAsync(ex);
                }
            }

            [ComponentInteraction("testroleselector.*", true)] //todo: cleanup
            public async Task TestSelectRoleResponseAsync(string rgid, string[] selections)
            {
                if (Context.User is not SocketGuildUser gUser)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden);
                    return;
                }

                if (selections.Length == 0 || !ulong.TryParse(rgid, out var parsedGid))
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput);
                    return;
                }

                var roleGroup = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.RgId == parsedGid);
                if (roleGroup == null)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                var dbRoles = roleGroup.Roles;
                var userRoleIds = gUser.Roles.Select(x => x.Id);
                var rolesToAdd = new List<DbRole>();
                var rolesToRemove = new List<DbRole>();
                var rolesInvalid = new List<string>();

                if (roleGroup.AllowOnlyOne)
                {
                    var dbRole = dbRoles.FirstOrDefault(x => x.Name == selections[0]);
                    if (dbRole != null)
                    {
                        var alreadyPossesedRoles = dbRoles.Where(x => userRoleIds.Contains(x.RId));
                        rolesToRemove.AddRange(alreadyPossesedRoles);
                        if(!rolesToRemove.Contains(dbRole))
                            rolesToAdd.Add(dbRole);
                    }
                    else
                        rolesInvalid.Add(selections[0]);
                }
                else
                {
                    foreach (var selection in selections)
                    {
                        var dbRole = dbRoles.FirstOrDefault(x => x.Name == selection);
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
                        await gUser.AddRolesAsync(rolesToAdd.Select(x => x.RId));
                        var rolesToAddText = string.Join(", ", rolesToAdd.Select(x => $"{x.Name}(<@&{x.RId}>)"));
                        groupFields.Add(new EmbedFieldBuilder()
                        {
                            IsInline = true,
                            Name = "Roles Added",
                            Value = rolesToAddText
                        });
                        _logger.LogInformation("Added roles {addedRoles} to user {userName}({userId})", rolesToAddText, gUser.Username, gUser.Id);
                    }
                    if (rolesToRemove.Any())
                    {
                        await gUser.RemoveRolesAsync(rolesToRemove.Select(x => x.RId));
                        var rolesToRemoveText = string.Join(", ", rolesToRemove.Select(x => $"{x.Name}(<@&{x.RId}>)"));
                        groupFields.Add(new EmbedFieldBuilder()
                        {
                            IsInline = true,
                            Name = "Roles Removed",
                            Value = rolesToRemoveText
                        });
                        _logger.LogInformation("Removed roles {removedRoles} from user {userName}({userId})", rolesToRemoveText, gUser.Username, gUser.Id);
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
                        _logger.LogWarning("Failed to find roles {invalidRoles} in group {roleGroup}({roleGroupId}), could not apply to user {userName}({userId})", rolesInvalidText, roleGroup.Name, roleGroup.RgId, gUser.Username, gUser.Id);
                    }

                    if (!groupFields.Any())
                    {
                        await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                        return;
                    }

                    var embedBuilder = new EmbedBuilder()
                    {
                        Fields = groupFields,
                        Color = Color.Blue,
                        Title = "Roles Updated"
                    };

                    await RespondEmbedAsync(embedBuilder.Build());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed responding to interaction");
                    await RespondErrorEmbedAsync(ex);
                }
            }

            #region Utility
            private static readonly Regex _roleNameVerificator = new(@"\A[A-Za-z \d]{2,20}\Z");
            private static bool IsIdentifierValid(string identifier)
                => _roleNameVerificator.IsMatch(identifier);
            #endregion
        }
    }
}
