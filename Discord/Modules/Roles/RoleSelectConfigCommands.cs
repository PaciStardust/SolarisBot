using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles
{
    [Module("roles/roleselect"), Group("cfg-roleselect", "[MANAGE ROLES ONLY] Role selection config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
    internal class RoleSelectConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<RoleSelectConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal RoleSelectConfigCommands(ILogger<RoleSelectConfigCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("view-all", "View all roles and groups (Including empty ones)")]
        public async Task ViewAllRolesAsync()
        {
            var roleGroups = await _dbContext.RoleGroups.ForGuildWithRoles(Context.Guild.Id).ToArrayAsync();

            if (roleGroups.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var strings = roleGroups.OrderBy(x => x.Identifier)
                .Select(x =>
                {
                    var title = $"{x.Identifier} ({(x.AllowOnlyOne ? "One of" : "Multi")}{(x.RequiredRoleId == 0 ? string.Empty : $", <@&{x.RequiredRoleId}> Only")})";
                    var rolesText = x.RoleConfigs.Any()
                        ? string.Join("\n", x.RoleConfigs.OrderBy(x => x.Identifier).Select(x => $"┗ {x.Identifier}(<@&{x.RoleId}>)"))
                        : "┗ (No roles assigned to group)";

                    return $"{title}\n{rolesText}";
                });

            await Interaction.ReplyAsync("List of Assignable Roles", string.Join("\n\n", strings));
        }

        [SlashCommand("group-create", "Create role group")]
        public async Task CreateRoleGroupAsync([MinLength(2), MaxLength(20)] string identifier, [MaxLength(200)] string description = "", bool oneof = true, IRole? requiredRole = null)
        {
            var identifierTrimmed = identifier.Trim();
            var descriptionTrimmed = description.Trim();
            if (!DiscordUtils.IsIdentifierValid(identifierTrimmed))
            {
                await Interaction.RespondInvalidIdentifierErrorEmbedAsync(identifierTrimmed);
                return;
            }
            if (descriptionTrimmed.Length > 200)
            {
                await Interaction.ReplyErrorAsync("Descriptions must be 200 characters or shorter");
                return;
            }

            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id, x => x.Include(y => y.RoleGroups));

            var identifierSearch = identifierTrimmed.ToLower();
            var roleGroup = guild.RoleGroups.FirstOrDefault(x => x.Identifier.ToLower() == identifierSearch)
                ?? new() { GuildId = Context.Guild.Id, Identifier = identifierTrimmed };

            var logVerb = roleGroup.RoleGroupId == ulong.MinValue ? "Creat" : "Updat";
            roleGroup.AllowOnlyOne = oneof;
            roleGroup.Description = descriptionTrimmed;
            roleGroup.RequiredRoleId = requiredRole?.Id ?? ulong.MinValue;

            _dbContext.RoleGroups.Update(roleGroup);

            _logger.LogDebug("{intTag} {verb}ing role group {roleGroup} for guild {guild}", GetIntTag(), logVerb, roleGroup, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} {verb}ed role group {roleGroup} for guild {guild}", GetIntTag(), logVerb, roleGroup, Context.Guild.Log());
            await Interaction.ReplyAsync($"Role group **\"{roleGroup.Identifier}\"** {logVerb.ToLower()}ed\n\nOne Of: **{(roleGroup.AllowOnlyOne ? "Yes" : "No")}**\nDescription: **{(string.IsNullOrWhiteSpace(roleGroup.Description) ? "None" : roleGroup.Description)}**\nRequired: **{(roleGroup.RequiredRoleId == ulong.MinValue ? "None" : $"<@&{roleGroup.RequiredRoleId}>")}**");
        }

        [SlashCommand("group-delete", "Delete a role group")]
        public async Task DeleteRoleGroupAsync([MinLength(2), MaxLength(20)] string identifier)
        {
            var identifierTrimmed = identifier.Trim();
            if (!DiscordUtils.IsIdentifierValid(identifierTrimmed))
            {
                await Interaction.RespondInvalidIdentifierErrorEmbedAsync(identifierTrimmed);
                return;
            }

            var identifierSearch = identifierTrimmed.ToLower();
            var match = await _dbContext.RoleGroups.ForGuild(Context.Guild.Id).FirstOrDefaultAsync(x => x.Identifier.ToLower() == identifierSearch);
            if (match is null)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _dbContext.RoleGroups.Remove(match);

            _logger.LogDebug("{intTag} Deleting role group {roleGroup} from guild {guild}", GetIntTag(), match, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Deleted role group {roleGroup} from guild {guild}", GetIntTag(), match, Context.Guild.Log());
            await Interaction.ReplyAsync("The role group with the identifier **\"{identifierTrimmed}\"** has been deleted");
        }

        [SlashCommand("role-register", "Register role group")]
        public async Task RegisterRoleAsync(IRole role, string group, [MinLength(2), MaxLength(20)] string identifier = "", [MaxLength(200)] string description = "")
        {
            if (string.IsNullOrWhiteSpace(identifier))
                identifier = role.Name;

            var descriptionTrimmed = description.Trim();
            var identifierTrimmed = identifier.Trim();
            var groupSearch = group.Trim().ToLower();

            var identifierValid = DiscordUtils.IsIdentifierValid(identifierTrimmed);
            if (!identifierValid || !DiscordUtils.IsIdentifierValid(groupSearch))
            {
                await Interaction.RespondInvalidIdentifierErrorEmbedAsync(identifierValid ? groupSearch : identifierTrimmed);
                return;
            }
            if (descriptionTrimmed.Length > 200)
            {
                await Interaction.ReplyErrorAsync("Descriptions must be 200 characters or shorter");
                return;
            }

            if (await _dbContext.RoleConfigs.FirstOrDefaultAsync(x => x.RoleId == role.Id) is not null)
            {
                await Interaction.ReplyErrorAsync("Role is already registered");
                return;
            }

            var roleGroup = await _dbContext.RoleGroups.ForGuildWithRoles(Context.Guild.Id).FirstOrDefaultAsync(x => x.Identifier.ToLower() == groupSearch);
            if (roleGroup is null)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var lowerIdentifierName = identifierTrimmed.ToLower();
            if (roleGroup.RoleConfigs.FirstOrDefault(x => x.Identifier.ToLower() == lowerIdentifierName) is not null)
            {
                await Interaction.ReplyErrorAsync("A Role with that identifier is already registered");
                return;
            }

            var dbRole = new DbRoleConfig()
            {
                Identifier = identifierTrimmed,
                RoleId = role.Id,
                RoleGroupId = roleGroup.RoleGroupId,
                Description = descriptionTrimmed
            };

            _dbContext.RoleConfigs.Add(dbRole);

            _logger.LogDebug("{intTag} Registering role {role} to group {roleGroup} in guild {guild}", GetIntTag(), dbRole, roleGroup, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Registered role {role} to group {roleGroup} in guild {guild}", GetIntTag(), dbRole, roleGroup, Context.Guild.Log());
            await Interaction.ReplyAsync($"Role **\"{dbRole.Identifier}\"** registered\n\nGroup: **{roleGroup.Identifier}**\nRole: **{role.Mention}**\nDescription: **{(string.IsNullOrWhiteSpace(dbRole.Description) ? "None" : dbRole.Description)}**");
        }

        [SlashCommand("role-unregister", "Unregister a role")]
        public async Task UnregisterRoleAsync([MinLength(2), MaxLength(20)] string group, [MinLength(2), MaxLength(20)] string identifier)
        {
            var groupSearch = group.Trim().ToLower();
            var identifierSearch = identifier.Trim().ToLower();

            var groupValid = DiscordUtils.IsIdentifierValid(groupSearch);
            if (!groupValid || !DiscordUtils.IsIdentifierValid(identifierSearch))
            {
                await Interaction.RespondInvalidIdentifierErrorEmbedAsync(groupValid ? identifierSearch : groupSearch);
                return;
            }

            var dbGroup = await _dbContext.RoleGroups.ForGuildWithRoles(Context.Guild.Id).FirstOrDefaultAsync(x => x.Identifier.ToLower() == groupSearch);
            var dbRole = dbGroup?.RoleConfigs.FirstOrDefault(x => x.Identifier.ToLower() == identifierSearch);
            if (dbRole is null)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _dbContext.RoleConfigs.Remove(dbRole);

            _logger.LogDebug("{intTag} Unregistering role {role} from groups", GetIntTag(), dbRole);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Unregistered role {role} from groups", GetIntTag(), dbRole);
            await Interaction.ReplyAsync($"A role with the identifier **\"{identifierSearch}\"** has been unregistered");
        }
    }
}
