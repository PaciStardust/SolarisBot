using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Text;

namespace SolarisBot.Discord.Modules.Roles.RoleSelect
{
    [Module("roles/roleselect"), Group("roleselect", "Role select related commands"), RequireContext(ContextType.Guild)]
    public sealed class RoleSelectCommands : SolarisInteractionModuleBase
    {
        private readonly RoleSelectService _rsService;
        internal RoleSelectCommands(RoleSelectService rsService)
        {
            _rsService = rsService;
        }

        [SlashCommand("view", "View all roles and groups")]
        public async Task ViewRolesAsync
        (
            [Summary(description: "[Opt] Visibility of result")] bool visible = false
        )
        {
            var roleGroups = await _rsService.GetRoleGroupsForGuildAsync(Context.Guild.Id);
            if (roleGroups.Length == 0)
            {
                await Interaction.ReplyErrorAsync(StandardError.NoResults);
                return;
            }

            var groupFields = new List<EmbedFieldBuilder>();
            foreach (var roleGroup in roleGroups.OrderBy(x => x.Identifier))
            {
                var roles = roleGroup.RoleConfigs;
                if (roles.Count == 0) continue;


                var featuresList = new List<string>();
                if (roleGroup.RequiredRoleId != ulong.MinValue)
                    featuresList.Add($"Requires <@&{roleGroup.RequiredRoleId}>");
                if (!roleGroup.AllowOnlyOne)
                    featuresList.Add("Multiselect");

                var valueTextBuilder = new StringBuilder();
                if (!string.IsNullOrWhiteSpace(roleGroup.Description))
                    valueTextBuilder.Append(roleGroup.Description);
                if (featuresList.Count != 0)
                {
                    if (valueTextBuilder.Length != 0)
                        valueTextBuilder.Append(" - ");
                    valueTextBuilder.Append(string.Join(", ", featuresList));
                }
                valueTextBuilder.AppendLine($"{(valueTextBuilder.Length == 0 ? "\n" : "\n\n")}Roles({roleGroup.RoleConfigs.Count})");
                valueTextBuilder.Append(string.Join("\n", roles.OrderBy(x => x.Identifier).Select(x => $"┗ {x.Identifier} => <@&{x.RoleId}>")));

                var fieldBuilder = new EmbedFieldBuilder()
                {
                    Name = roleGroup.Identifier,
                    Value = valueTextBuilder.ToString(),
                    IsInline = true
                };
                groupFields.Add(fieldBuilder);
            }

            if (groupFields.Count == 0)
            {
                await Interaction.ReplyErrorAsync(StandardError.NoResults);
                return;
            }

            var embedBuilder = EmbedFactory.Builder()
                .WithTitle("Self-Assignable Roles")
                .WithFields(groupFields)
                .WithFooter($"Use \"/roleselect select *[groupname/rolename]*\" to pick roles from a group");

            await Interaction.ReplyAsync(embedBuilder.Build(), !visible);
        }

        [SlashCommand("select", "Select roles from a group"), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task SelectRolesAsync
        (
            [Summary(description: "Identifier of group or role"), MinLength(2), MaxLength(20)] string identifier
        )
        {
            var res = await _rsService.SelectRoleGroupAsync(Context.User, identifier);
            await res.Match(
                success => Interaction.ReplyComponentAsync(RoleSelectService.GenerateRoleGroupSelector(success.Value), $"Roles in group {success.Value.Identifier}:", true),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [ComponentInteraction("solaris_roleselector.*", true), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task SelectRoleResponseAsync(string rgid, string[] selections)
        {
            var res = await _rsService.HandleRoleSelectorInteractionAsync(Context.User, rgid, selections);
            await res.Match(
                success => Interaction.ReplyAsync(success.Value, isEphemeral: true),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
