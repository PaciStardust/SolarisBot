using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Fun
{
    [Module("fun/renaming"), Group("cfg-rename", "[MANAGE NAMES ONLY] Renaming config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageNicknames), RequireUserPermission(GuildPermission.ManageNicknames)]
    public sealed class RenamingConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<RenamingConfigCommands> _logger;
        private readonly DbService _dbService;
        internal RenamingConfigCommands(ILogger<RenamingConfigCommands> logger, DbService dbService)
        {
            _dbService = dbService;
            _logger = logger;
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
                await Interaction.ReplyInvalidParameterErrorAsync("min timeout");
                return;
            }
            if (!ulong.TryParse(maxTimeout, out var parsedMaxTimeout))
            {
                await Interaction.ReplyInvalidParameterErrorAsync("max timeout");
                return;
            }

            using var dbCtx = _dbService.GetContext();
            var guild = await dbCtx.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.JokeRenameOn = enabled;
            guild.JokeRenameTimeoutMax = parsedMaxTimeout;
            guild.JokeRenameTimeoutMin = parsedMinTimeout > parsedMaxTimeout ? parsedMaxTimeout : parsedMinTimeout;

            _logger.LogDebug("{intTag} Setting joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", GetIntTag(), enabled, parsedMinTimeout, parsedMaxTimeout, Context.Guild.Log());
            await dbCtx.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", GetIntTag(), enabled, parsedMinTimeout, parsedMaxTimeout, Context.Guild.Log());
            await Interaction.ReplyAsync($"Joke Renaming is currently **{(enabled ? "enabled" : "disabled")}**\n\nTime: **{parsedMinTimeout} - {parsedMaxTimeout} seconds**");
        }

        [SlashCommand("reset", "Reset joke rename cooldowns")]
        public async Task RenameResetCooldownsAsync()
        {
            _logger.LogDebug("{intTag} Deleting all joke timeout cooldowns for guild {guild}", GetIntTag(), Context.Guild.Log());
            using var dbCtx = _dbService.GetContext();
            var deleted = await dbCtx.JokeTimeouts.ForGuild(Context.Guild.Id).ExecuteDeleteAsync();
            _logger.LogInformation("{intTag} Deleted all {delCount} joke timeout cooldowns for guild {guild}", GetIntTag(), deleted, Context.Guild.Log());
            if (deleted == 0)
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
            else
                await Interaction.ReplyAsync($"Successfully deleted all **{deleted}** joke timeouts for this guild");
        }
    }
}
