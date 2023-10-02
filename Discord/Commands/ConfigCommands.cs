using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Commands
{
    [Group("config", "[ADMIN ONLY] Configure other Solaris features"), DefaultMemberPermissions(GuildPermission.Administrator), RequireUserPermission(GuildPermission.Administrator)]
    public sealed class ConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<ConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal ConfigCommands(ILogger<ConfigCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }
        protected override ILogger? GetLogger() => _logger;

        [SlashCommand("vouching", "Set up vouching (Not setting either disables vouching)")]
        public async Task ConfigVouchingAsync(IRole? permission, IRole? vouch)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.VouchPermissionRoleId = permission?.Id ?? 0;
            guild.VouchRoleId = vouch?.Id ?? 0;

            _logger.LogDebug("{intTag} Setting vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", GetIntTag(), permission?.Log() ?? "0", vouch?.Log() ?? "0", Context.Guild.Log());
            var (_, err) = await _dbContext.TrySaveChangesAsync();
            if (err != null)
            {
                _logger.LogError(err,"{intTag} Failed to set vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", GetIntTag(), permission?.Log() ?? "0", vouch?.Log() ?? "0", Context.Guild.Log());
                await RespondErrorEmbedAsync(err);
                return;
            }
            _logger.LogInformation("{intTag} Set vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", GetIntTag(), permission?.Log() ?? "0", vouch?.Log() ?? "0", Context.Guild.Log());
            await RespondEmbedAsync("Vouching Configured", $"Vouching is currently **{(permission != null && vouch != null ? "enabled" : "disabled")}**\n\nPermission: **{permission?.Mention ?? "None"}**\nVouch: **{vouch?.Mention ?? "None"}**");
        }

        [SlashCommand("magic", "Set up magic role (Not setting role disables it)")]
        public async Task ConfigureMagicAsync(IRole? role = null, ulong timeoutsecs = 1800, bool renaming = false)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.MagicRoleId = role?.Id ?? 0;
            guild.MagicRoleNextUse = 0;
            guild.MagicRoleTimeout = timeoutsecs >= 0 ? timeoutsecs : 0;
            guild.MagicRoleRenameOn = renaming;

            _logger.LogDebug("{intTag} Setting magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", GetIntTag(), role?.Log() ?? "0", guild.MagicRoleTimeout, guild.MagicRoleRenameOn, Context.Guild.Log());
            var (_, err) = await _dbContext.TrySaveChangesAsync();
            if (err != null)
            {
                _logger.LogError(err, "{intTag} Failed to set magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", GetIntTag(), role?.Log() ?? "0", guild.MagicRoleTimeout, guild.MagicRoleRenameOn, Context.Guild.Log());
                await RespondErrorEmbedAsync(err);
                return;
            }
            _logger.LogInformation("{intTag} Set magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", GetIntTag(), role?.Log() ?? "0", guild.MagicRoleTimeout, guild.MagicRoleRenameOn, Context.Guild.Log());
            await RespondEmbedAsync("Magic Configured", $"Magic is currently **{(role != null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**\nTimeout: **{guild.MagicRoleTimeout} seconds**\nRenaming: **{guild.MagicRoleRenameOn}**");
        }

        [SlashCommand("joke-rename", "Set up joke renaming (Timeout in seconds)")]
        public async Task ConfigureJokeRenameAsync(bool enabled, [MinValue(0), MaxValue(2.628e+6)] ulong mintimeout = 1800, [MinValue(0), MaxValue(2.628e+6)] ulong maxtimeout = 86400)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.JokeRenameOn = enabled;
            guild.JokeRenameTimeoutMax = maxtimeout;
            guild.JokeRenameTimeoutMin = mintimeout > maxtimeout ? maxtimeout : mintimeout;

            _logger.LogDebug("{intTag} Setting joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", GetIntTag(), enabled, mintimeout, maxtimeout, Context.Guild.Log());
            var (_, err) = await _dbContext.TrySaveChangesAsync();
            if (err != null)
            {
                _logger.LogError(err, "{intTag} Failed to set joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", GetIntTag(), enabled, mintimeout, maxtimeout, Context.Guild.Log());
                await RespondErrorEmbedAsync(err);
                return;
            }
            _logger.LogInformation("{intTag} Set joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", GetIntTag(), enabled, mintimeout, maxtimeout, Context.Guild.Log());
            await RespondEmbedAsync("Joke Renaming Configured", $"Joke Renaming is currently **{(enabled ? "enabled" : "disabled")}**\n\nTime: **{mintimeout} - {maxtimeout} seconds**");
        }

        [SlashCommand("joke-rename-reset", "Reset joke rename cooldowns")]
        public async Task JokeRenameResetCooldownsAsync()
        {
            try
            {
                _logger.LogDebug("{intTag} Deleting all joke timeout cooldowns for guild {guild}", GetIntTag(), Context.Guild.Log());
                var deleted = await _dbContext.JokeTimeouts.Where(x => x.GuildId == Context.Guild.Id).ExecuteDeleteAsync();
                _logger.LogInformation("{intTag} Deleted all {delCount} joke timeout cooldowns for guild {guild}", GetIntTag(), deleted, Context.Guild.Log());
                if (deleted == 0)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                else
                    await RespondEmbedAsync("Joke Timeouts Deleted", $"Successfully deleted all **{deleted}** joke timeouts for this guild");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{intTag} Failed to delete all joke timeout cooldowns for guild {guild}", GetIntTag(), Context.Guild.Log());
                await RespondErrorEmbedAsync(ex);
            }
        }

        [SlashCommand("auto-role", "Set a join role")]
        public async Task SetAutoRoleAsync(IRole? role = null)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);
            guild.AutoRoleId = role?.Id ?? 0;

            _logger.LogDebug("{intTag} Setting auto-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            var (_, err) = await _dbContext.TrySaveChangesAsync();
            if (err != null)
            {
                _logger.LogError(err, "{intTag} Failed to set auto-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
                await RespondErrorEmbedAsync(err);
                return;
            }
            _logger.LogInformation("{intTag} Set auto-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await RespondEmbedAsync("Auto-Role Configured", $"Auto-Role is currently **{(role != null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**");
        }
    }
}
