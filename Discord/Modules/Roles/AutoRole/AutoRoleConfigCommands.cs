using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles.AutoRole
{
    [Module("roles/autorole")]
    public sealed class AutoRoleConfigCommands : SolarisInteractionModuleBase
    {
        private readonly AutoRoleService _roleService;

        internal AutoRoleConfigCommands(AutoRoleService roleService)
        {
            _roleService = roleService;
        }

        [SlashCommand("cfg-autorole", "[MANAGE ROLES ONLY] Set an automatic join role"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task SetAutoRoleAsync
        (
            [Summary(description: "[Opt] Join role (none to disable)")] IRole? role = null
        )
        {
            var res = await _roleService.ConfigAutoRoleAsync(Context.Guild, role);
            await res.Match(
                success => Interaction.ReplyAsync($"Auto-Role is currently **{(role is not null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
