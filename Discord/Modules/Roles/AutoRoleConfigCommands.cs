using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles
{
    [Module("roles/autorole")]
    public sealed class AutoRoleConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<AutoRoleConfigCommands> _logger;
        private readonly DatabaseService _dbService;

        internal AutoRoleConfigCommands(ILogger<AutoRoleConfigCommands> logger, DatabaseService dbService)
        {
            _logger = logger;
            _dbService = dbService;
        }

        [SlashCommand("cfg-autorole", "[MANAGE ROLES ONLY] Set an automatic join role"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task SetAutoRoleAsync
        (
            [Summary(description: "[Opt] Join role (none to disable)")] IRole? role = null
        )
        {
            using var dbCtx = _dbService.GetContext();
            var guild = await dbCtx.GetOrCreateTrackedGuildAsync(Context.Guild.Id);
            guild.AutoRoleId = role?.Id ?? ulong.MinValue;

            _logger.LogDebug("{intTag} Setting auto-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await dbCtx.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set auto-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await Interaction.ReplyAsync($"Auto-Role is currently **{(role is not null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**");
        }
    }
}
