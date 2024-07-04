using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles.Vouch
{
    [Module("roles/vouch")]
    public sealed class VouchCommands : SolarisInteractionModuleBase
    {
        private readonly VouchService _vouchService;

        internal VouchCommands(VouchService vouchService)
        {
            _vouchService = vouchService;
        }

        [SlashCommand("vouch", "Vouch for a user"), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task VouchUserAsync(IUser user)
        {
            var res = await _vouchService.VouchUserAsync(Context.Guild, Context.User, user);
            await res.Match(
                success => Interaction.ReplyAsync(success.Value),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
