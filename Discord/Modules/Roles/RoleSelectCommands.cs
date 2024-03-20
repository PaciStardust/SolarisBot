using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Text;

namespace SolarisBot.Discord.Modules.Roles
{
    [Module("roles/roleselect"), Group("roleselect", "Role select related commands"), RequireContext(ContextType.Guild)]
    public sealed class RoleSelectCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<RoleSelectCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal RoleSelectCommands(ILogger<RoleSelectCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("view", "View all roles and groups")]
        public async Task ViewRolesAsync()
        {
            var roleGroups = await _dbContext.RoleGroups.ForGuildWithRoles(Context.Guild.Id).ToArrayAsync();

            if (roleGroups.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var groupFields = new List<EmbedFieldBuilder>();
            foreach (var roleGroup in roleGroups.OrderBy(x => x.Identifier))
            {
                var roles = roleGroup.RoleConfigs;
                if (!roles.Any()) continue;


                var featuresList = new List<string>();
                if (roleGroup.RequiredRoleId != 0)
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
                valueTextBuilder.AppendLine($"{(valueTextBuilder.Length == 0 ? "\n" : "\n\n")}Roles({roleGroup.RoleConfigs.Count})");
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
                .WithFooter($"Use \"/roles select *[groupname/rolename]*\" to pick roles from a group");

            await Interaction.ReplyAsync(embedBuilder.Build());
        }

        [SlashCommand("select", "Select roles from a group"), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task SelectRolesAsync
        (
            [Summary(description: "Identifier of group or role"), MinLength(2), MaxLength(20)] string identifier
        )
        {
            var gUser = GetGuildUser(Context.User);
            var identifierSearch = identifier.Trim();
            if (!DiscordUtils.IsIdentifierValid(identifierSearch))
            {
                await Interaction.RespondInvalidIdentifierErrorEmbedAsync(identifier);
                return;
            }

            var roleGroups = await _dbContext.RoleGroups.ForGuildWithRoles(Context.Guild.Id).ToArrayAsync();
            var roleGroupMatch = RoleSelectHelper.FindRoleGroupForIdentifier(roleGroups, identifier);

            var roleCount = roleGroupMatch?.RoleConfigs.Count ?? 0;
            if (roleCount == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            if (roleGroupMatch!.RequiredRoleId != 0 && !gUser.Roles.Select(x => x.Id).Contains(roleGroupMatch.RequiredRoleId))
            {
                await Interaction.ReplyErrorAsync($"You do not have the required role <@&{roleGroupMatch.RequiredRoleId}>");
                return;
            }

            if (roleCount == 1)
            {
                var resultEmbed = await AssignRolesToUser(gUser, roleGroupMatch.RoleConfigs);
                await Interaction.ReplyAsync(resultEmbed, isEphemeral: true);
                return;
            }

            var component = RoleSelectHelper.GenerateRoleGroupSelector(roleGroupMatch);
            await Interaction.ReplyComponentAsync(component, $"Roles in group {roleGroupMatch.Identifier}:", true);
        }

        [ComponentInteraction("solaris_roleselector.*", true), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task SelectRoleResponseAsync(string rgid, string[] selections)
        {
            var gUser = GetGuildUser(Context.User);
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

            var roleGroup = await _dbContext.RoleGroups.ForGuildWithRoles(gUser.Guild.Id).FirstOrDefaultAsync(x => x.RoleGroupId == parsedGid);
            if (roleGroup is null)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            if (roleGroup.RequiredRoleId != 0 && !gUser.Roles.Any(x => x.Id == roleGroup.RequiredRoleId))
            {
                await Interaction.ReplyErrorAsync($"You do not have the required role <@&{roleGroup.RequiredRoleId}>");
                return;
            }

            var dbRoles = roleGroup.RoleConfigs;
            var invalidRoles = new List<string>();
            var selectedRoles = new List<DbRoleConfig>();
            if (roleGroup.AllowOnlyOne)
                selections = selections[0..1]; //remove all but first
            foreach (var selection in selections)
            {
                var match = dbRoles.FirstOrDefault(x => x.Identifier == selection);
                if (match is null)
                    invalidRoles.Add(selection);
                else
                    selectedRoles.Add(match);
            }

            var resultEmbed = await AssignRolesToUser(gUser, selectedRoles, invalidRoles);
            await Interaction.ReplyAsync(resultEmbed, isEphemeral: true);
        }

        private async Task<Embed> AssignRolesToUser(SocketGuildUser gUser, IEnumerable<DbRoleConfig>? roleConfigs = null, IEnumerable<string>? rolesInvalid = null)
        {
            var groupFields = new List<EmbedFieldBuilder>();

            if (roleConfigs?.Any() ?? false)
            {
                var userRoleIds = gUser.Roles.Select(x => x.Id);
                var rolesToAdd = new List<DbRoleConfig>();
                var rolesToRemove = new List<DbRoleConfig>();

                foreach (var roleConfig in roleConfigs)
                {
                    if (userRoleIds.Contains(roleConfig.RoleId))
                        rolesToRemove.Add(roleConfig);
                    else
                        rolesToAdd.Add(roleConfig);
                }

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
            }

            if (rolesInvalid?.Any() ?? false)
            {
                var rolesInvalidText = string.Join(", ", rolesInvalid);
                groupFields.Add(new EmbedFieldBuilder()
                {
                    IsInline = true,
                    Name = "Invalid Roles",
                    Value = rolesInvalidText
                });
                _logger.LogWarning("{intTag} Failed to find roles {invalidRoles} role list, could not apply to user {userData}", GetIntTag(), rolesInvalidText, gUser.Log());
            }

            if (!groupFields.Any())
                return EmbedFactory.Error(GenericError.NoResults);

            var embedBuilder = EmbedFactory.Builder()
                .WithTitle("Roles Updated")
                .WithFields(groupFields);

            return embedBuilder.Build();
        }
    }
}
