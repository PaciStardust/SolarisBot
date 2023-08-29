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

        [SlashCommand("view", "View all roles and groups")]
        public async Task ViewRolesAsync()
        {
            var roleGroups = _dbContext.RoleGroups.Where(x => x.GId == Context.Guild.Id);

            if (!roleGroups.Any())
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

        [SlashCommand("select", "Select roles from a group")]
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
                _logger.LogError(ex, "Failed to respond to interaction");
                await RespondErrorEmbedAsync(ex, isEphemeral: true);
            }
        }

        [ComponentInteraction("roleselector.*", true)]
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

            var roleGroup = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.RgId == parsedGid);
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
                    await gUser.AddRolesAsync(rolesToAdd.Select(x => x.RId));
                    var rolesToAddText = string.Join(", ", rolesToAdd.Select(x => $"{x.Identifier}(<@&{x.RId}>)"));
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
                    var rolesToRemoveText = string.Join(", ", rolesToRemove.Select(x => $"{x.Identifier}(<@&{x.RId}>)"));
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
                    _logger.LogWarning("Failed to find roles {invalidRoles} in group {roleGroup}({roleGroupId}), could not apply to user {userName}({userId})", rolesInvalidText, roleGroup.Identifier, roleGroup.RgId, gUser.Username, gUser.Id);
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
                _logger.LogError(ex, "Failed responding to interaction");
                await RespondErrorEmbedAsync(ex, isEphemeral: true);
            }
        }
    }
}
