using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using System.Text;

namespace SolarisBot.Discord.Commands
{
    [Group("roles", "Role related commands"), RequireContext(ContextType.Guild)] //todo: [FEATURE] Color of the day Role, permanent role selectors?
    public sealed class RoleCommands : SolarisInteractionModuleBase //todo: [BUG] Better tooltip for select
    {
        private readonly ILogger<RoleCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal RoleCommands(ILogger<RoleCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        #region Admin
        [SlashCommand("view-all", "[MANAGE ROLES ONLY] View all roles and groups (Including empty ones)"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task ViewAllRolesAsync()
        {
            var roleGroups = await _dbContext.RoleGroups.Where(x => x.GuildId == Context.Guild.Id).ToArrayAsync();

            if (roleGroups.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var strings = roleGroups.OrderBy(x => x.Identifier)
                .Select(x =>
                {
                    var title = $"{x.Identifier} ({(x.AllowOnlyOne ? "One of" : "Multi")}{(x.RequiredRoleId == 0 ? string.Empty : $", <@&{x.RequiredRoleId}> Only")})";
                    var rolesText = x.Roles.Any()
                        ? string.Join("\n", x.Roles.OrderBy(x => x.Identifier).Select(x => $"┗ {x.Identifier}(<@&{x.RoleId}>)"))
                        : "┗ (No roles assigned to group)";

                    return $"{title}\n{rolesText}";
                });

            await Interaction.ReplyAsync("List of Assignable Roles", string.Join("\n\n", strings));
        }

        [SlashCommand("group-create", "[MANAGE ROLES ONLY] Create role group"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task CreateRoleGroupAsync([MinLength(2), MaxLength(20)] string identifier, [MaxLength(200)] string description = "", bool oneof = true, IRole? requiredRole = null)
        {
            var identifierTrimmed = identifier.Trim();
            var descriptionTrimmed = description.Trim();
            if (!DiscordUtils.IsIdentifierValid(identifierTrimmed))
            {
                await ReplyInvalidIdentifierErrorEmbedAsync(identifierTrimmed);
                return;
            }
            if (descriptionTrimmed.Length > 200)
            {
                await Interaction.ReplyErrorAsync("Descriptions must be 200 characters or shorter");
                return;
            }

            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

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

        [SlashCommand("group-delete", "[MANAGE ROLES ONLY] Delete a role group"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task DeleteRoleGroupAsync([MinLength(2), MaxLength(20)] string identifier)
        {
            var identifierTrimmed = identifier.Trim();
            if (!DiscordUtils.IsIdentifierValid(identifierTrimmed))
            {
                await ReplyInvalidIdentifierErrorEmbedAsync(identifierTrimmed);
                return;
            }

            var identifierSearch = identifierTrimmed.ToLower();
            var match = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.Identifier.ToLower() == identifierSearch);
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

        [SlashCommand("role-register", "[MANAGE ROLES ONLY] Register role group"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
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
                await ReplyInvalidIdentifierErrorEmbedAsync(identifierValid ? groupSearch : identifierTrimmed);
                return;
            }
            if (descriptionTrimmed.Length > 200)
            {
                await Interaction.ReplyErrorAsync("Descriptions must be 200 characters or shorter");
                return;
            }

            if (await _dbContext.RoleSettings.FirstOrDefaultAsync(x => x.RoleId == role.Id) is not null)
            {
                await Interaction.ReplyErrorAsync("Role is already registered");
                return;
            }

            var roleGroup = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.Identifier.ToLower() == groupSearch);
            if (roleGroup is null)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var lowerIdentifierName = identifierTrimmed.ToLower();
            if (roleGroup.Roles.FirstOrDefault(x => x.Identifier.ToLower() == lowerIdentifierName) is not null)
            {
                await Interaction.ReplyErrorAsync("A Role with that identifier is already registered");
                return;
            }

            var dbRole = new DbRoleSettings()
            {
                Identifier = identifierTrimmed,
                RoleId = role.Id,
                RoleGroupId = roleGroup.RoleGroupId,
                Description = descriptionTrimmed
            };

            _dbContext.RoleSettings.Add(dbRole);

            _logger.LogDebug("{intTag} Registering role {role} to group {roleGroup} in guild {guild}", GetIntTag(), dbRole, roleGroup, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Registered role {role} to group {roleGroup} in guild {guild}", GetIntTag(), dbRole, roleGroup, Context.Guild.Log());
            await Interaction.ReplyAsync($"Role **\"{dbRole.Identifier}\"** registered\n\nGroup: **{roleGroup.Identifier}**\nRole: **{role.Mention}**\nDescription: **{(string.IsNullOrWhiteSpace(dbRole.Description) ? "None" : dbRole.Description)}**");
        }

        [SlashCommand("role-unregister", "[MANAGE ROLES ONLY] Unregister a role"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task UnregisterRoleAsync([MinLength(2), MaxLength(20)] string group, [MinLength(2), MaxLength(20)] string identifier)
        {
            var groupSearch = group.Trim().ToLower();
            var identifierSearch = identifier.Trim().ToLower();

            var groupValid = DiscordUtils.IsIdentifierValid(groupSearch);
            if (!groupValid || !DiscordUtils.IsIdentifierValid(identifierSearch))
            {
                await ReplyInvalidIdentifierErrorEmbedAsync(groupValid ? identifierSearch : groupSearch);
                return;
            }

            var dbGroup = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.Identifier.ToLower() == groupSearch);
            var dbRole = dbGroup?.Roles.FirstOrDefault(x => x.Identifier.ToLower() == identifierSearch);
            if (dbRole is null)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _dbContext.RoleSettings.Remove(dbRole);

            _logger.LogDebug("{intTag} Unregistering role {role} from groups", GetIntTag(), dbRole);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Unregistered role {role} from groups", GetIntTag(), dbRole);
            await Interaction.ReplyAsync($"A role with the identifier **\"{identifierSearch}\"** has been unregistered");
        }
        #endregion

        #region User
        [SlashCommand("view", "View all roles and groups")]
        public async Task ViewRolesAsync()
        {
            var roleGroups = await _dbContext.RoleGroups.Where(x => x.GuildId == Context.Guild.Id).ToArrayAsync();

            if (roleGroups.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var groupFields = new List<EmbedFieldBuilder>();
            foreach (var roleGroup in roleGroups.OrderBy(x => x.Identifier))
            {
                var roles = roleGroup.Roles;
                if (!roles.Any()) continue;


                var featuresList = new List<string>();
                if (roleGroup.RequiredRoleId != ulong.MinValue)
                    featuresList.Add($"Requires <@&{roleGroup.RequiredRoleId}>");
                if (!roleGroup.AllowOnlyOne)
                    featuresList.Add("Multiselect");

                var valueTextBuilder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(roleGroup.Description))
                    valueTextBuilder.Append(roleGroup.Description);
                if (featuresList.Any())
                {
                    if (valueTextBuilder.Length != 0)
                        valueTextBuilder.Append(" - ");
                    valueTextBuilder.Append(string.Join(", ", featuresList));
                }
                valueTextBuilder.AppendLine($"{(valueTextBuilder.Length == 0 ? "\n" : "\n\n")}Roles({roleGroup.Roles.Count})");
                valueTextBuilder.Append(string.Join("\n", roles.OrderBy(x => x.Identifier).Select(x => $"┗ {x.Identifier}(<@&{x.RoleId}>)")));

                var fieldBuilder = new EmbedFieldBuilder()
                {
                    Name = roleGroup.Identifier,
                    Value = valueTextBuilder.ToString(),
                    IsInline = true
                };
                groupFields.Add(fieldBuilder);
            }

            if (!groupFields.Any())
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var embedBuilder = EmbedFactory.Builder()
                .WithTitle("Self-Assignable Roles")
                .WithFields(groupFields)
                .WithFooter("Use \"/roles select\" to pick roles");

            await Interaction.ReplyAsync(embedBuilder.Build());
        }

        [SlashCommand("select", "Select roles from a group"), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task SelectRolesAsync([MinLength(2), MaxLength(20)] string group)
        {
            var gUser = GetGuildUser()!;
            var groupSearch = group.Trim().ToLower();
            if (!DiscordUtils.IsIdentifierValid(groupSearch))
            {
                await ReplyInvalidIdentifierErrorEmbedAsync(group);
                return;
            }

            var dbGroup = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GuildId == Context.Guild.Id && x.Identifier.ToLower() == groupSearch);
            var roles = dbGroup?.Roles;
            if (roles is null || !roles.Any())
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            if (dbGroup!.RequiredRoleId != ulong.MinValue && !gUser.Roles.Select(x => x.Id).Contains(dbGroup.RequiredRoleId))
            {
                await Interaction.ReplyErrorAsync(GenericError.Forbidden);
                return;
            }

            var menuBuilder = new SelectMenuBuilder()
            {
                CustomId = $"roleselector.{dbGroup.RoleGroupId}",
                Placeholder = dbGroup.AllowOnlyOne ? "Select a role..." : "Select roles...",
                MaxValues = dbGroup.AllowOnlyOne ? 1 : roles.Count,
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
            await RespondAsync($"Roles in group {dbGroup.Identifier}:", components: compBuilder.Build(), ephemeral: true);
        }

        [ComponentInteraction("roleselector.*", true), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task SelectRoleResponseAsync(string rgid, string[] selections)
        {
            var gUser = GetGuildUser()!;
            if (selections.Length == 0)
            {
                await Interaction.ReplyErrorAsync("No selections have been made");
                return;
            }
            if (!ulong.TryParse(rgid, out var parsedGid))
            {
                await Interaction.ReplyErrorAsync($"Could not parse RGId **{rgid}**");
                return;
            }

            var roleGroup = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.RoleGroupId == parsedGid && x.GuildId == gUser.Guild.Id);
            if (roleGroup is null)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var dbRoles = roleGroup.Roles;
            var userRoleIds = gUser.Roles.Select(x => x.Id);
            var rolesToAdd = new List<DbRoleSettings>();
            var rolesToRemove = new List<DbRoleSettings>();
            var rolesInvalid = new List<string>();

            if (roleGroup.AllowOnlyOne)
            {
                var dbRole = dbRoles.FirstOrDefault(x => x.Identifier == selections[0]);
                if (dbRole is not null)
                {
                    var alreadyPossesedRoles = dbRoles.Where(x => userRoleIds.Contains(x.RoleId));
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
                    if (dbRole is null)
                    {
                        rolesInvalid.Add(selection);
                        continue;
                    }

                    if (userRoleIds.Contains(dbRole.RoleId))
                        rolesToRemove.Add(dbRole);
                    else
                        rolesToAdd.Add(dbRole);
                }
            }

            var groupFields = new List<EmbedFieldBuilder>();

            if (rolesToAdd.Any())
            {
                var rolesToAddText = string.Join(", ", rolesToAdd.Select(x => $"{x.Identifier}(<@&{x.RoleId}>)"));
                _logger.LogDebug("{intTag} Adding roles {addedRoles} to user {userData} in guild {guild}", GetIntTag(), rolesToAddText, gUser.Log(), Context.Guild.Log());
                await gUser.AddRolesAsync(rolesToAdd.Select(x => x.RoleId));
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
                var rolesToRemoveText = string.Join(", ", rolesToRemove.Select(x => $"{x.Identifier}(<@&{x.RoleId}>)"));
                _logger.LogDebug("{intTag} Removing roles {removedRoles} from user {userData} in guild {guild}", GetIntTag(), rolesToRemoveText, gUser.Log(), Context.Guild.Log());
                await gUser.RemoveRolesAsync(rolesToRemove.Select(x => x.RoleId));
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
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var embedBuilder = EmbedFactory.Builder()
                .WithTitle("Roles Updated")
                .WithFields(groupFields);

            await Interaction.ReplyAsync(embedBuilder.Build(), isEphemeral: true);
        }
        #endregion

        #region Utils
        private async Task ReplyInvalidIdentifierErrorEmbedAsync(string identifier) 
            =>  await Interaction.ReplyErrorAsync($"Identifier **{identifier}** is invalid, identifiers can only contain letters, numbers, and spaces and must be between 2 and 20 characters long");
        #endregion
    }
}
