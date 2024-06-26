using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles.UtilityRoles
{
    [Module("roles/magic")]
    public sealed class MagicCommands : SolarisInteractionModuleBase
    {
        private readonly VouchService _roleService;
        internal MagicCommands(VouchService roleService)
        {
            _roleService = roleService;
        }

        [SlashCommand("cfg-magic", "[MANAGE ROLES ONLY] Set up magic role"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task ConfigureMagicAsync
        (
            [Summary(description: "[Opt] Magic role (none to disable)")] IRole? role = null,
            [Summary(description: "[Opt] Command cooldown (in sec)")] string timeout = "1800",
            [Summary(description: "[Opt] Automatically rename role?")] bool renaming = false
        )
        {
            if (!ulong.TryParse(timeout, out var parsedTimeout))
            {
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("timeout"));
                return;
            }

            var res = await _roleService.ConfigMagicAsync(Context.Guild, role, parsedTimeout, renaming);
            await res.Match(
                success => Interaction.ReplyAsync($"Magic is currently **{(role is not null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**\nTimeout: **{success.Value.MagicRoleTimeout} seconds**\nRenaming: **{success.Value.MagicRoleRenameOn}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("magic", "Use magic"), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task UseMagicAsync()
        {
            var res = await _roleService.UseMagicAsync(Context.Guild);
            await res.Match(
                success => Interaction.ReplyAsync($"Magic has been used, {success.Value.Mention} feels different now", success.Value.Color),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
