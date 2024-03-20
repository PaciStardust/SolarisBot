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
        private readonly DatabaseContext _dbContext;
        internal AutoRoleConfigCommands(ILogger<AutoRoleConfigCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("cfg-autorole", "[MANAGE ROLES ONLY] Set an automatic join role"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task SetAutoRoleAsync
        (
            [Summary(description: "Join role (none to disable)")] IRole? role = null
        )
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);
            guild.AutoRoleId = role?.Id ?? 0;

            _logger.LogDebug("{intTag} Setting auto-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set auto-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await Interaction.ReplyAsync($"Auto-Role is currently **{(role is not null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**");
        }
    }
}
