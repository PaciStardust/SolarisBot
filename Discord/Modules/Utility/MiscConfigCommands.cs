using Discord.Interactions;
using Discord;
using SolarisBot.Discord.Common;

namespace SolarisBot.Discord.Modules.Utility
{
    internal class MiscConfigCommands : SolarisInteractionModuleBase
    {
        private readonly MiscConfigService _configService;

        internal MiscConfigCommands(MiscConfigService configService)
        {
            _configService = configService;
        }

        [SlashCommand("cfg-errordm", "Set DMing for errors"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task ConfigErrorDmAsync(bool enabled)
        {
            var res = await _configService.SetErrorDmAsync(Context.Guild, enabled);
            await res.Match(
                success => Interaction.ReplyAsync($"DM error notifying is currently **{(success.Value.DisableErrorDm ? "disabled" : "enabled")}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
