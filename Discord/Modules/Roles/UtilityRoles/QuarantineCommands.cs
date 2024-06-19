using Discord.Interactions;
using Discord;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles.UtilityRoles
{
    [Module("roles/quarantine")]
    internal class QuarantineCommands : SolarisInteractionModuleBase
    {
        private readonly UtilityRoleService _roleService;
        internal QuarantineCommands(UtilityRoleService roleService)
        {
            _roleService = roleService;
        }

        [SlashCommand("cfg-quarantine", "[MANAGE ROLES ONLY] Set up quarantine"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task ConfigQuarantineAsync
        (
            [Summary(description: "[Opt] Role aquired through quarantine (none to disable)")] IRole? role = null
        )
        {
            var res = await _roleService.ConfigQuarantineAsync(Context.Guild, role);
            await res.Match(
                success => Interaction.ReplyAsync($"Quarantine is currently **{(role is null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [UserCommand("Quarantine"), SlashCommand("quarantine", "Quarantine a user"), RequireBotPermission(GuildPermission.ManageRoles), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task QuarantineUserAsync(IUser user)
        {
            var res = await _roleService.QuarantineUserAsync(Context.Guild, Context.User, user);
            await res.Match(
                success => Interaction.ReplyAsync($"{user.Mention} {(success.Value ? "has been" : "is no longer")} quarantined"),
                deletedRole => Interaction.ReplyDeletedRoleErrorAsync(deletedRole.Value),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
