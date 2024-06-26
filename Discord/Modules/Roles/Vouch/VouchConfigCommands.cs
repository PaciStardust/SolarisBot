using Discord.Interactions;
using Discord;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Common;

namespace SolarisBot.Discord.Modules.Roles.Vouch
{
    [Module("roles/vouch"), Group("cfg-vouch", "[MANAGE ROLES ONLY] Set up vouching")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
    public sealed class VouchConfigCommands : SolarisInteractionModuleBase //todo: [FEATURE] Custom message
    {
        private readonly VouchService _vouchService;

        internal VouchConfigCommands(VouchService vouchService)
        {
            _vouchService = vouchService;
        }

        [SlashCommand("config", "Set up vouching")]
        public async Task ConfigVouchingAsync
        (
            [Summary(description: "[Opt] Role required for vouching (none to disable)")] IRole? permission = null,
            [Summary(description: "[Opt] Role aquired through vouching (none to disable)")] IRole? vouch = null
        )
        {
            var res = await _vouchService.ConfigVouchingAsync(Context.Guild, permission, vouch);
            await res.Match(
                success => Interaction.ReplyAsync($"Vouching is currently **{(permission is not null && vouch is not null ? "enabled" : "disabled")}**\n\nPermission: **{permission?.Mention ?? "None"}**\nVouch: **{vouch?.Mention ?? "None"}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
