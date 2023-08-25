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
                if (!DiscordUtils.IsIdentifierValid(nameClean) || descriptionClean.Length > 200)
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
                if (!DiscordUtils.IsIdentifierValid(nameClean))
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

                if (!DiscordUtils.IsIdentifierValid(identifierNameClean) || !DiscordUtils.IsIdentifierValid(groupNameCleanLower) || descriptionClean.Length > 200)
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

                if (!DiscordUtils.IsIdentifierValid(identifierNameClean))
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
        }
    }
}
