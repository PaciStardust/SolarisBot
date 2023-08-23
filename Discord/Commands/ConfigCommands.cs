using Discord;
using Discord.Interactions;
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

                var strings = guild.RoleGroups.OrderBy(x => x.Name)
                    .Select(x =>
                    {
                        var title = $"{x.Name} ({(x.AllowOnlyOne ? "Single" : "Multi")})";
                        var rolesText = x.Roles.Any()
                            ? string.Join("\n", x.Roles.OrderBy(x => x.Name).Select(x => $"┗{x.Name}(<@&{x.RId}>)"))
                            : "┗(No roles assigned to group)";

                        return $"{title}\n{rolesText}";
                    });

                await RespondEmbedAsync("List of Assignable Roles", string.Join("\n\n", strings));
            }

            private static readonly Regex _roleNameVerificator = new(@"\A[a-z 0-9]{2,20}\Z");

            [SlashCommand("create-group", "Create a role group (Group names can only be made of 2-20 letters, numbers, and spaces)")]
            public async Task CreateRoleGroupAsync(string name, bool allowMultiple = true)
            {
                var nameClean = name.Trim().ToLower();
                if (!_roleNameVerificator.IsMatch(nameClean))
                {
                    await RespondErrorEmbedAsync("Invalid Name", "Group names must be made of letters, numbers, and spaces and can only be 2-20 characters long");
                    return;
                }

                var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

                if (guild.RoleGroups.FirstOrDefault(x => x.Name == nameClean) != null)
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
                var nameClean = name.Trim().ToLower();
                if (!_roleNameVerificator.IsMatch(nameClean))
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
                var roleGroup = guild?.RoleGroups.FirstOrDefault(x => x.Name == nameClean);
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

            [SlashCommand("register-role", "Register a role to a group (Identifier names can only be made of 2-20 letters, numbers, and spaces)")]
            public async Task RegisterRoleAsync(IRole role, string identifier, string group)
            {
                var identifierNameClean = identifier.Trim().ToLower();
                var groupNameClean = group.Trim().ToLower();

                if (!_roleNameVerificator.IsMatch(identifierNameClean) || !_roleNameVerificator.IsMatch(groupNameClean))
                {
                    await RespondErrorEmbedAsync("Invalid Identifier", "Group and identifier must be made of letters, numbers, and spaces and can only be 2-20 characters long");
                    return;
                }

                var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
                var roleGroup = guild?.RoleGroups.FirstOrDefault(x => x.Name == groupNameClean);
                if (roleGroup == null)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                if (_dbContext.Roles.FirstOrDefault(x => x.RId == role.Id) != null)
                {
                    await RespondErrorEmbedAsync("Already Registered", "Role is already registered");
                    return;
                }

                var dbRole = new DbRole()
                {
                    Name = identifierNameClean,
                    RId = role.Id,
                    RgId = roleGroup.RgId
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

                if (!_roleNameVerificator.IsMatch(identifierNameClean))
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                var role = _dbContext.Roles.FirstOrDefault(x => x.Name == identifierNameClean);
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
        }
        // todo: [FEATURE] Allow uppercase
        // todo: [FEATURE] Group Descripton
    }
}
