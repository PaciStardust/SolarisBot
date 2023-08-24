using Discord;
using Discord.Interactions;
using Discord.WebSocket;
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
                var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);

                if (guild == null || !guild.RoleGroups.Any())
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                //todo: [FEATURE] Remove special on other removal?
                //todo: [FEATURE] Role requirement for tag?

                var strings = guild.RoleGroups.OrderBy(x => x.Name)
                    .Select(x =>
                    {
                        var title = $"{x.Name} ({(x.AllowOnlyOne ? "Single" : "Multi")})";
                        var rolesText = x.Roles.Any()
                            ? string.Join("\n", x.Roles.OrderBy(x => x.Name).Select(x => $"┗ {x.Name}(<@&{x.RId}>)"))
                            : "┗ (No roles assigned to group)";

                        return $"{title}\n{rolesText}";
                    });

                await RespondEmbedAsync("List of Assignable Roles", string.Join("\n\n", strings));
            }

            [SlashCommand("create-group", "Create a role group (Group names can only be made of 2-20 letters, numbers, and spaces)")] //todo: override with same name
            public async Task CreateRoleGroupAsync([MinLength(2), MaxLength(20)] string name, bool allowMultiple = false)
            {
                var nameClean = name.Trim();
                if (!IsIdentifierValid(nameClean))
                {
                    await RespondErrorEmbedAsync("Invalid Name", "Group names must be made of letters, numbers, and spaces");
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
                    Name = nameClean
                };

                await _dbContext.RoleGroups.AddAsync(roleGroup);

                if (await _dbContext.TrySaveChangesAsync() == -1)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                else
                {
                    _logger.LogInformation("Created role group {groupName} for guild {guildId}", roleGroup.Name, roleGroup.GId);
                    await RespondEmbedAsync("Role Group Created", $"A role group with the name \"{nameClean}\" has been created");
                }
            }

            [SlashCommand("delete-group", "Delete a role group")]
            public async Task DeleteRoleGroupAsync(string name)
            {
                var nameClean = name.Trim();
                if (!IsIdentifierValid(nameClean))
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
                var lowerName = nameClean.ToLower();
                var roleGroup = guild?.RoleGroups.FirstOrDefault(x => x.Name.ToLower() == lowerName);
                if (roleGroup == null)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                _dbContext.RoleGroups.Remove(roleGroup);

                if (await _dbContext.TrySaveChangesAsync() == -1)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                else
                {
                    _logger.LogInformation("Deleted role group {groupName} from guild {guildId}", roleGroup.Name, roleGroup.GId);
                    await RespondEmbedAsync("Role Group Deleted", $"A role group with the name \"{nameClean}\" has been deleted");
                }
            }

            [SlashCommand("register-role", "Register a role to a group (Identifier names can only be made of 2-20 letters, numbers, and spaces)")] //todo: override with same name
            public async Task RegisterRoleAsync(IRole role, [MinLength(2), MaxLength(20)] string identifier, string group, [MinLength(2), MaxLength(200)] string description = "")
            {
                var descriptionClean = description.Trim();
                var identifierNameClean = identifier.Trim();
                var groupNameCleanLower = group.Trim().ToLower();

                if (!IsIdentifierValid(identifierNameClean) || !IsIdentifierValid(groupNameCleanLower) || descriptionClean.Length > 200)
                {
                    await RespondErrorEmbedAsync("Invalid Identifier", "Group and identifier must be made of letters, numbers, and spaces");
                    return;
                }

                if (_dbContext.Roles.FirstOrDefault(x => x.RId == role.Id) != null)
                {
                    await RespondErrorEmbedAsync("Already Registered", "Role is already registered");
                    return;
                }

                var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
                var roleGroup = guild?.RoleGroups.FirstOrDefault(x => x.Name.ToLower() == groupNameCleanLower);
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
                    Description = description
                };

                await _dbContext.Roles.AddAsync(dbRole);

                if (await _dbContext.TrySaveChangesAsync() == -1)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                else
                {
                    _logger.LogInformation("Role with identifier {roleName} registered to group {groupName} in guild {guildId}", dbRole.Name, roleGroup.Name, roleGroup.GId);
                    await RespondEmbedAsync("Role Registered", $"A role with the identifier \"{identifierNameClean}\" has been registered");
                }
            }

            [SlashCommand("unregister-role", "Unregister a role")]
            public async Task UnregisterRoleAsync(string identifier)
            {
                var identifierNameClean = identifier.Trim().ToLower();

                if (!IsIdentifierValid(identifierNameClean))
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                var role = _dbContext.Roles.FirstOrDefault(x => x.Name.ToLower() == identifierNameClean);
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
                    _logger.LogInformation("Role with identifier {roleName} unregistered from group {groupName}", role.Name, role.Name);
                    await RespondEmbedAsync("Role Unegistered", $"A role with the identifier \"{identifierNameClean}\" has been unregistered");
                }
            }

            [SlashCommand("role-dropdown", "ye")]
            public async Task RoleDropdownTestAsync()
            {
                var guild = _dbContext.Guilds.FirstOrDefault(x => x.GId == Context.Guild.Id);

                if (guild == null)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                int matches = 0;
                var menuBuilder = new SelectMenuBuilder()
                {
                    CustomId = "roledropdowntest",
                    Placeholder = "Select Roles...",
                    MaxValues = 1,
                    Type = ComponentType.SelectMenu
                };

                foreach (var roleGroup in guild.RoleGroups.OrderBy(x => x.Name))
                {
                    foreach (var role in roleGroup.Roles.OrderBy(x => x.Name))
                    {
                        matches++;
                        var desc = role.Description;
                        if (string.IsNullOrWhiteSpace(desc))
                            desc = role.Name;
                        menuBuilder.AddOption($"{role.Name} ({roleGroup.Name})", role.RId.ToString(), desc);
                    }
                }

                if (matches == 0)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                try
                {
                    await RespondAsync(components: new ComponentBuilder().WithSelectMenu(menuBuilder).Build());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to respond to interaction");
                }
            }

            [ComponentInteraction("roledropdowntest", true)]
            public async Task RoleDropdownTestResponseAsync(string[] selections) //todo: more precise logging
            {
                if (Context.User is not SocketGuildUser gUser)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NotGuild);
                    return;
                }

                var selected = selections.FirstOrDefault();
                if (!ulong.TryParse(selected, out var roleId))
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                var role = _dbContext.Roles.FirstOrDefault(x => x.RId == roleId);
                if (role == null || role.RoleGroup.GId != Context.Guild.Id)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                var userRoleIds = gUser.Roles.Select(x => x.Id);
                try
                {
                    //Remove role if owned
                    if (userRoleIds.Contains(roleId))
                    {
                        await gUser.RemoveRoleAsync(roleId);
                        await RespondEmbedAsync("Role Removed", $"Successfully removed role \"{role.Name}\"");
                        _logger.LogInformation("Removed role {removedRole} from user {userName}({userId})", roleId, gUser.Username, gUser.Id);
                        return;
                    }

                    //Remove all other roles from group
                    if (role.RoleGroup.AllowOnlyOne)
                    {
                        var roleGroupIds = role.RoleGroup.Roles.Select(x => x.RId);
                        var rolesToRemove = userRoleIds.Intersect(roleGroupIds);
                        if (rolesToRemove.Any())
                        {
                            await gUser.RemoveRolesAsync(rolesToRemove);
                            _logger.LogInformation("Removed roles {removedRoles} from user {userName}({userId})", rolesToRemove, gUser.Username, gUser.Id);
                        }
                    }

                    await gUser.AddRoleAsync(role.RId);
                    await RespondEmbedAsync("Role Added", $"Successfully added role \"{role.Name}\"");
                    _logger.LogInformation("Added role {addedRole} to user {userName}({userId})", roleId, gUser.Username, gUser.Id);
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed responding to interaction");
                    await RespondEmbedAsync(DiscordUtils.EmbedError(ex));
                }
            }

            private static readonly Regex _roleNameVerificator = new(@"\A[A-Za-z \d]{2,20}\Z");
            private static bool IsIdentifierValid(string identifier)
                => _roleNameVerificator.IsMatch(identifier);
        }
    }
}
