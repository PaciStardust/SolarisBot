using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Commands
{
    [Group("config", "[ADMIN ONLY] Configure other Solaris features"), RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.Administrator)]
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

            _logger.LogDebug("Setting vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", permission?.Log() ?? "0", vouch?.Log() ?? "0", Context.Guild.Log());
            if (await _dbContext.SaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Set vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", permission?.Log() ?? "0", vouch?.Log() ?? "0", Context.Guild.Log());
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

            _logger.LogDebug("Setting magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", role?.Log() ?? "0", guild.MagicRoleTimeout, guild.MagicRoleRenameOn, Context.Guild.Log());
            if (await _dbContext.SaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Set magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", role?.Log() ?? "0", guild.MagicRoleTimeout, guild.MagicRoleRenameOn, Context.Guild.Log());
            await RespondEmbedAsync("Magic Configured", $"Magic is currently **{(role != null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**\nTimeout: **{guild.MagicRoleTimeout} seconds**\nRenaming: **{guild.MagicRoleRenameOn}**");
        }

        [SlashCommand("joke-rename", "Set up joke renaming (Timeout in seconds)")]
        public async Task ConfigureJokeRenameAsync(bool enabled, [MinValue(0), MaxValue(2.628e+6)] ulong mintimeout = 1800, [MinValue(0), MaxValue(2.628e+6)] ulong maxtimeout = 86400)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.JokeRenameOn = enabled;
            guild.JokeRenameTimeoutMax = maxtimeout;
            guild.JokeRenameTimeoutMin = mintimeout > maxtimeout ? maxtimeout : mintimeout;

            _logger.LogDebug("Setting joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", enabled, mintimeout, maxtimeout, Context.Guild.Log());
            if (await _dbContext.SaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Set joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", enabled, mintimeout, maxtimeout, Context.Guild.Log());
            await RespondEmbedAsync("Joke Renaming Configured", $"Joke Renaming is currently **{(enabled ? "enabled" : "disabled")}**\n\nTime: **{mintimeout} - {maxtimeout} seconds**");
        }

        [SlashCommand("joke-rename-reset", "Reset joke rename cooldowns")]
        public async Task JokeRenameResetCooldownsAsync()
        {
            try
            {
                _logger.LogDebug("Deleting all joke timeout cooldowns for guild {guild}", Context.Guild.Log());
                var deleted = await _dbContext.JokeTimeouts.Where(x => x.GId == Context.Guild.Id).ExecuteDeleteAsync();
                _logger.LogInformation("Deleted all {delCount} joke timeout cooldowns for guild {guild}", deleted, Context.Guild.Log());
                if (deleted == 0)
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                else
                    await RespondEmbedAsync("Joke Timeouts Deleted", $"Successfully deleted all **{deleted}** joke timeouts for this guild");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete all joke timeout cooldowns for guild {guild}", Context.Guild.Log());
                await RespondErrorEmbedAsync(ex);
            }
        }

        [SlashCommand("auto-role", "Set a join role")]
        public async Task SetAutoRoleAsync(IRole? role)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);
            guild.AutoRoleId = role?.Id ?? 0;

            _logger.LogDebug("Setting auto-role to role {role} for guild {guild}", role?.Log() ?? "0", Context.Guild.Log());
            if (await _dbContext.SaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Set auto-role to role {role} for guild {guild}", role?.Log() ?? "0", Context.Guild.Log());
            await RespondErrorEmbedAsync("Auto-Role Configured", $"Auto-Role is currently **{(role != null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**");
        }
    }
}
