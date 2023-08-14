using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Commands
{
    [Group("config", "[ADMIN ONLY] Configure Solaris"), RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.Administrator)] //todo: shortform responses
    public sealed class ConfigCommands : InteractionModuleBase
    {
        [Group("roles", "Role Configuration")]
        public sealed class ConfigRoleCommands : InteractionModuleBase
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
                    await RespondAsync(embed: DiscordUtils.NoResultsEmbed());
                    return;
                }

                //todo: remove from list on delete
                //todo: remove special on other removal?

                var strings = guild.RoleTags.OrderBy(x => x.Name)
                    .Select(x =>
                    {
                        var title = $"{x.Name} ({(x.AllowOnlyOne ? "Single" : "Multi")})";
                        var rolesText = x.Roles.Any()
                            ? string.Join(", ", x.Roles.OrderBy(x => x.Name).Select(x => $"{x.Name}({x.RId})"))
                            : "No roles assigned to tag";

                        return $"{title}\n{rolesText}";
                    });

                var responseEmbed = DiscordUtils.ResponseEmbed("List of Assignable Roles", string.Join("\n\n", strings));
                await RespondAsync(embed: responseEmbed);
            }

            private static readonly Regex _roleNameVerificator = new(@"\A[a-z ]{2,20}\Z");

            [SlashCommand("create-tag", "Create a role tag (Tag names can only be made of 2-20 letters and spaces)")]
            public async Task CreateRoleTagAsync(string name, bool allowMultiple = true)
            {
                var nameClean = name.Trim().ToLower();
                if (!_roleNameVerificator.IsMatch(nameClean))
                {
                    var responseEmbed = DiscordUtils.ErrorEmbed("Invalid Name", "Tag names must be made of letters and spaces and can only be 2-20 characters long");
                    await RespondAsync(embed: responseEmbed);
                    return;
                }

                var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

                if (guild.RoleTags.Where(x => x.Name == nameClean).Any())
                {
                    var responseEmbed = DiscordUtils.ErrorEmbed("Already Exists", $"A tag by the name of \"{nameClean}\" already exists");
                    await RespondAsync(embed: responseEmbed);
                    return;
                }

                var roleTag = new DbRoleTag() //todo: do default values cause issues here?
                {
                    AllowOnlyOne = allowMultiple,
                    GId = Context.Guild.Id,
                    Name = nameClean
                };

                guild.RoleTags.Add(roleTag);

                var changes = await _dbContext.TrySaveChangesAsync();
                var responseEmbedFinal = changes != -1
                    ? DiscordUtils.ResponseEmbed("Role Tag Created", $"A role tag with the name \"{nameClean}\" has been created")
                    : DiscordUtils.DatabaseErrorEmbed();
                await RespondAsync(embed: responseEmbedFinal);
            }

            [SlashCommand("delete-tag", "Delete a role tag")]
            public async Task DeleteRoleTagAsync(string name)
            {
                var nameClean = name.Trim().ToLower();
                if (!_roleNameVerificator.IsMatch(nameClean))
                {
                    var responseEmbed = DiscordUtils.NoResultsEmbed();
                    await RespondAsync(embed: responseEmbed);
                    return;
                }

                var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
                var roleTag = guild?.RoleTags.FirstOrDefault(x => x.Name == nameClean);
                if (roleTag == null)
                {
                    var responseEmbed = DiscordUtils.NoResultsEmbed();
                    await RespondAsync(embed: responseEmbed);
                    return;
                }

                _dbContext.RoleTags.Remove(roleTag);
                var changes = await _dbContext.TrySaveChangesAsync();
                var responseEmbedFinal = changes != -1
                    ? DiscordUtils.ResponseEmbed("Role Tag Deleted", $"A role tag with the name \"{nameClean}\" has been deleted")
                    : DiscordUtils.DatabaseErrorEmbed();
                await RespondAsync(embed: responseEmbedFinal);
            }

            [SlashCommand("add-role", "Add a role to a tag (Identifier names can only be made of 2-20 letters and spaces)")]
            public async Task AddRoleAsync(IRole role, string identifier, string tag)
            {
                var identifierNameClean = identifier.Trim().ToLower();
                var tagNameClean = tag.Trim().ToLower();

                if (!_roleNameVerificator.IsMatch(identifierNameClean) || !_roleNameVerificator.IsMatch(tagNameClean))
                {
                    var responseEmbed = DiscordUtils.ErrorEmbed("Invalid Name", "Name and identifier must be made of letters and spaces and can only be 2-20 characters long");
                    await RespondAsync(embed: responseEmbed);
                    return;
                }

                var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
                if (roleTag == null)
                {
                    var responseEmbed = DiscordUtils.NoResultsEmbed();
                    await RespondAsync(embed: responseEmbed);
                    return;
                }
            }
        }
    }
}
