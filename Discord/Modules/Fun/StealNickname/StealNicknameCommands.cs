using Discord.Interactions;
using Discord;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Common;

namespace SolarisBot.Discord.Modules.Fun.StealNickname
{
    [Module("fun/stealnickname")]
    internal class StealNicknameCommands : SolarisInteractionModuleBase
    {
        private readonly StealNicknameService _snService;
        internal StealNicknameCommands(StealNicknameService snService)
        {
            _snService = snService;
        }

        [SlashCommand("cfg-stealnick", "[MANAGE NICKS ONLY] Set up nickname stealing"), RequireBotPermission(GuildPermission.ManageNicknames), RequireUserPermission(GuildPermission.ManageNicknames)]
        public async Task ConfigureStealNicknameAsync
        (
            [Summary(description: "Is feature enabled?")] bool enabled
        )
        {
            var res = await _snService.ConfigureStealNicknameAsync(Context.Guild, enabled);
            await res.Match(
                success => Interaction.ReplyAsync($"Nickname stealing is currently **{(success.Value.StealNicknameOn ? "enabled" : "disabled")}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("stealnick", "Steal a persons nick"), RequireBotPermission(GuildPermission.ManageNicknames)]
        public async Task StealNicknameUserAsync(IUser user)
        {
            var res = await _snService.StealNicknameAsync(Context.User, user);
            await res.Match(
                success => Interaction.ReplyAsync($"**{success.Value.Item1}** *({Context.User.Mention})* stole the letter **{success.Value.Item3}** from **{success.Value.Item2}** *({user.Mention})*"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
