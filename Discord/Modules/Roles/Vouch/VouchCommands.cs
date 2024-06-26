using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles.Vouch
{
    [Module("roles/vouch")]
    public sealed class VouchCommands : SolarisInteractionModuleBase
    {
        private readonly VouchService _roleService;

        internal VouchCommands(VouchService roleService) //todo: [FEATURE] Custom message
        {
            _roleService = roleService;
        }

        [SlashCommand("cfg-vouch", "[MANAGE ROLES ONLY] Set up vouching"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task ConfigVouchingAsync
        (
            [Summary(description: "[Opt] Role required for vouching (none to disable)")] IRole? permission = null,
            [Summary(description: "[Opt] Role aquired through vouching (none to disable)")] IRole? vouch = null
        )
        {
            var res = await _roleService.ConfigVouchingAsync(Context.Guild, permission, vouch);
            await res.Match(
                success => Interaction.ReplyAsync($"Vouching is currently **{(permission is not null && vouch is not null ? "enabled" : "disabled")}**\n\nPermission: **{permission?.Mention ?? "None"}**\nVouch: **{vouch?.Mention ?? "None"}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [UserCommand("Vouch"), SlashCommand("vouch", "Vouch for a user"), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task VouchUserAsync(IUser user)
        {
            var res = await _roleService.VouchUserAsync(Context.Guild, Context.User, user);
            await res.Match(
                success => Interaction.ReplyAsync($"Vouched for {user.Mention}, welcome to the server!"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
