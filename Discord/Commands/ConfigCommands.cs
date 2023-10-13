using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Commands
{
    [Group("config", "[MANAGE GUILD ONLY] Configure other Solaris features"), DefaultMemberPermissions(GuildPermission.ManageGuild), RequireUserPermission(GuildPermission.ManageGuild)]
    public sealed class ConfigCommands : SolarisInteractionModuleBase //todo: [FEATURE] How do other bots handle permission stuff?
    {
        private readonly ILogger<ConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal ConfigCommands(ILogger<ConfigCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("vouching", "Set up vouching (Not setting either disables vouching)")]
        public async Task ConfigVouchingAsync(IRole? permission, IRole? vouch)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.VouchPermissionRoleId = permission?.Id ?? ulong.MinValue;
            guild.VouchRoleId = vouch?.Id ?? ulong.MinValue;

            _logger.LogDebug("{intTag} Setting vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", GetIntTag(), permission?.Log() ?? "0", vouch?.Log() ?? "0", Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", GetIntTag(), permission?.Log() ?? "0", vouch?.Log() ?? "0", Context.Guild.Log());
            await Interaction.ReplyAsync($"Vouching is currently **{(permission is not null && vouch is not null ? "enabled" : "disabled")}**\n\nPermission: **{permission?.Mention ?? "None"}**\nVouch: **{vouch?.Mention ?? "None"}**");
        }

        [SlashCommand("magic", "Set up magic role (Not setting role disables it)")]
        public async Task ConfigureMagicAsync(IRole? role = null, ulong timeoutsecs = 1800, bool renaming = false)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.MagicRoleId = role?.Id ?? ulong.MinValue;
            guild.MagicRoleNextUse = ulong.MinValue;
            guild.MagicRoleTimeout = timeoutsecs >= ulong.MinValue ? timeoutsecs : ulong.MinValue;
            guild.MagicRoleRenameOn = renaming;

            _logger.LogDebug("{intTag} Setting magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", GetIntTag(), role?.Log() ?? "0", guild.MagicRoleTimeout, guild.MagicRoleRenameOn, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", GetIntTag(), role?.Log() ?? "0", guild.MagicRoleTimeout, guild.MagicRoleRenameOn, Context.Guild.Log());
            await Interaction.ReplyAsync($"Magic is currently **{(role is not null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**\nTimeout: **{guild.MagicRoleTimeout} seconds**\nRenaming: **{guild.MagicRoleRenameOn}**");
        }

        [SlashCommand("joke-rename", "Set up joke renaming (Timeout in seconds)")]
        public async Task ConfigureJokeRenameAsync(bool enabled, [MinValue(ulong.MinValue), MaxValue(2.628e+6)] ulong mintimeout = 1800, [MinValue(ulong.MinValue), MaxValue(2.628e+6)] ulong maxtimeout = 86400)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.JokeRenameOn = enabled;
            guild.JokeRenameTimeoutMax = maxtimeout;
            guild.JokeRenameTimeoutMin = mintimeout > maxtimeout ? maxtimeout : mintimeout;

            _logger.LogDebug("{intTag} Setting joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", GetIntTag(), enabled, mintimeout, maxtimeout, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", GetIntTag(), enabled, mintimeout, maxtimeout, Context.Guild.Log());
            await Interaction.ReplyAsync($"Joke Renaming is currently **{(enabled ? "enabled" : "disabled")}**\n\nTime: **{mintimeout} - {maxtimeout} seconds**");
        }

        [SlashCommand("joke-rename-reset", "Reset joke rename cooldowns")]
        public async Task JokeRenameResetCooldownsAsync()
        {
            _logger.LogDebug("{intTag} Deleting all joke timeout cooldowns for guild {guild}", GetIntTag(), Context.Guild.Log());
            var deleted = await _dbContext.JokeTimeouts.Where(x => x.GuildId == Context.Guild.Id).ExecuteDeleteAsync();
            _logger.LogInformation("{intTag} Deleted all {delCount} joke timeout cooldowns for guild {guild}", GetIntTag(), deleted, Context.Guild.Log());
            if (deleted == 0)
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
            else
                await Interaction.ReplyAsync($"Successfully deleted all **{deleted}** joke timeouts for this guild");
        }

        [SlashCommand("auto-role", "Set a join role")]
        public async Task SetAutoRoleAsync(IRole? role = null)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);
            guild.AutoRoleId = role?.Id ?? ulong.MinValue;

            _logger.LogDebug("{intTag} Setting auto-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set auto-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await Interaction.ReplyAsync($"Auto-Role is currently **{(role is not null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**");
        }

        [SlashCommand("spellcheck", "Set a spellcheck role")]
        public async Task SetSpellcheckRoleAsync(IRole? role = null)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);
            guild.SpellcheckRoleId = role?.Id ?? ulong.MinValue;

            _logger.LogDebug("{intTag} Setting spellcheck-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set spellcheck-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await Interaction.ReplyAsync($"Spellcheck is currently **{(role is not null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**");
        }
    }
}
