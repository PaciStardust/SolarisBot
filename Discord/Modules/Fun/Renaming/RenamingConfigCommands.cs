using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Fun.Renaming
{
    [Module("fun/renaming"), Group("cfg-rename", "[MANAGE NAMES ONLY] Renaming config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageNicknames), RequireUserPermission(GuildPermission.ManageNicknames)]
    public sealed class RenamingConfigCommands : SolarisInteractionModuleBase
    {
        private readonly RenamingService _renamingService;
        internal RenamingConfigCommands(RenamingService renamingService)
        {
            _renamingService = renamingService;
        }

        [SlashCommand("config", "Set up joke renaming (Timeout in seconds)")]
        public async Task ConfigureRenameAsync
        (
            [Summary(description: "Is feature enabled?")] bool enabled,
            [Summary(description: "[Opt] Minimum time between renaming (in sec)")] string minTimeout = "1800",
            [Summary(description: "[Opt] Maximum time between renaming (in sec)")] string maxTimeout = "86400"
        )
        {
            if (!ulong.TryParse(minTimeout, out var parsedMinTimeout))
            {
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("min timeout"));
                return;
            }
            if (!ulong.TryParse(maxTimeout, out var parsedMaxTimeout))
            {
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("max timeout"));
                return;
            }

            var res = await _renamingService.ConfigureRenamingAsync(Context.Guild, enabled, parsedMinTimeout, parsedMaxTimeout);
            await res.Match(
                success => Interaction.ReplyAsync($"Joke Renaming is currently **{(success.Value.JokeRenameOn ? "enabled" : "disabled")}**\n\nTime: **{success.Value.JokeRenameTimeoutMin} - {success.Value.JokeRenameTimeoutMax} seconds**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("reset", "Reset joke rename cooldowns")]
        public async Task RenameResetCooldownsAsync()
        {
            var res = await _renamingService.ResetRenamingCooldownsAsync(Context.Guild);
            await res.Match(
                success => Interaction.ReplyAsync($"Successfully deleted all **{success.Value.Length}** joke timeouts for this guild"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
