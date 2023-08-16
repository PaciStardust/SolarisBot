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

            [SlashCommand("list", "List all roles and tags")]
            public async Task ListRolesAsync()
            {
                var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);

                if (guild == null || !guild.RoleTags.Any())
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                //todo: [FEATURE] Remove from list on delete
                //todo: [FEATURE] Remove special on other removal?

                var strings = guild.RoleTags.OrderBy(x => x.Name)
                    .Select(x =>
                    {
                        var title = $"{x.Name} ({(x.AllowOnlyOne ? "Single" : "Multi")})";
                        var rolesText = x.Roles.Any()
                            ? string.Join("\n", x.Roles.OrderBy(x => x.Name).Select(x => $"┗{x.Name}(<@&{x.RId}>)"))
                            : "┗(No roles assigned to tag)";

                        return $"{title}\n{rolesText}";
                    });

                await RespondEmbedAsync("List of Assignable Roles", string.Join("\n\n", strings));
            }

            private static readonly Regex _roleNameVerificator = new(@"\A[a-z ]{2,20}\Z");

            [SlashCommand("create-tag", "Create a role tag (Tag names can only be made of 2-20 letters and spaces)")]
            public async Task CreateRoleTagAsync(string name, bool allowMultiple = true)
            {
                var nameClean = name.Trim().ToLower();
                if (!_roleNameVerificator.IsMatch(nameClean))
                {
                    await RespondErrorEmbedAsync("Invalid Name", "Tag names must be made of letters and spaces and can only be 2-20 characters long");
                    return;
                }

                var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

                if (guild.RoleTags.FirstOrDefault(x => x.Name == nameClean) != null)
                {
                    await RespondErrorEmbedAsync("Already Exists", $"A tag by the name of \"{nameClean}\" already exists");
                    return;
                }

                var roleTag = new DbRoleTag()
                {
                    AllowOnlyOne = allowMultiple,
                    GId = Context.Guild.Id,
                    Name = nameClean
                };

                await _dbContext.RoleTags.AddAsync(roleTag);

                if (await _dbContext.TrySaveChangesAsync() == -1)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                else
                {
                    _logger.LogInformation("Created role tag {tagName} for guild {guildId}", roleTag.Name, roleTag.GId);
                    await RespondEmbedAsync("Role Tag Created", $"A role tag with the name \"{nameClean}\" has been created");
                }
            }

            [SlashCommand("delete-tag", "Delete a role tag")]
            public async Task DeleteRoleTagAsync(string name)
            {
                var nameClean = name.Trim().ToLower();
                if (!_roleNameVerificator.IsMatch(nameClean))
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
                var roleTag = guild?.RoleTags.FirstOrDefault(x => x.Name == nameClean);
                if (roleTag == null)
                {
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                    return;
                }

                _dbContext.RoleTags.Remove(roleTag);

                if (await _dbContext.TrySaveChangesAsync() == -1)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                else
                {
                    _logger.LogInformation("Deleted role tag {tagName} from guild {guildId}", roleTag.Name, roleTag.GId);
                    await RespondEmbedAsync("Role Tag Deleted", $"A role tag with the name \"{nameClean}\" has been deleted");
                }
            }

            [SlashCommand("register-role", "Register a role to a tag (Identifier names can only be made of 2-20 letters and spaces)")]
            public async Task RegisterRoleAsync(IRole role, string identifier, string tag)
            {
                var identifierNameClean = identifier.Trim().ToLower();
                var tagNameClean = tag.Trim().ToLower();

                if (!_roleNameVerificator.IsMatch(identifierNameClean) || !_roleNameVerificator.IsMatch(tagNameClean))
                {
                    await RespondErrorEmbedAsync("Invalid Identifier", "Tag and identifier must be made of letters and spaces and can only be 2-20 characters long");
                    return;
                }

                var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
                var roleTag = guild?.RoleTags.FirstOrDefault(x => x.Name == tagNameClean);
                if (roleTag == null)
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
                    TId = roleTag.TId
                };

                await _dbContext.Roles.AddAsync(dbRole);

                if (await _dbContext.TrySaveChangesAsync() == -1)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                else
                {
                    _logger.LogInformation("Role with identifier {roleName} registered to tag {tagName} in guild {guildId}", dbRole.Name, roleTag.Name, roleTag.GId);
                    await RespondEmbedAsync("Role Registered", $"A role with the identifier \"{identifierNameClean}\" has been registered");
                }
            }

            [SlashCommand("unregister-role", "Unregister a role")]
            public async Task UnregisterRoleAsync(string identifier)
            {
                var identifierNameClean = identifier.Trim().ToLower();

                if (!_roleNameVerificator.IsMatch(identifierNameClean))
                {
                    await RespondErrorEmbedAsync("Invalid Identifier", "Identifier must be made of letters and spaces and can only be 2-20 characters long");
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
                    _logger.LogInformation("Role with identifier {roleName} unregistered from tag {tagName}", role.Name, role.Name);
                    await RespondEmbedAsync("Role Unegistered", $"A role with the identifier \"{identifierNameClean}\" has been unregistered");
                }
            }

            //todo: [FEATURE] Ease of deletion w roles
            //todo: [FEATURE] Rename of tags to groups
        }
    }
}
