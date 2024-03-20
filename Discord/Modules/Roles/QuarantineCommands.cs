using Discord.Interactions;
using Discord;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles
{
    [Module("roles/quarantine")]
    internal class QuarantineCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<QuarantineCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal QuarantineCommands(ILogger<QuarantineCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("cfg-quarantine", "[MANAGE ROLES ONLY] Set up quarantine"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task ConfigQuarantineAsync
        (
            [Summary(description: "[Opt] Role aquired through quarantine (none to disable)")] IRole? role = null
        )
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.QuarantineRoleId = role?.Id ?? ulong.MinValue;

            _logger.LogDebug("{intTag} Setting quarantine to role={role} in guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set quarantine to role={role} in guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await Interaction.ReplyAsync($"Quarantine is currently **{(role is null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**");
        }

        [UserCommand("Quarantine"), SlashCommand("quarantine", "Quarantine a user"), RequireBotPermission(GuildPermission.ManageRoles), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task QuarantineUserAsync(IUser user)
        {
            var dbGuild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
            var gUser = GetGuildUser(Context.User);
            var gTargetUser = GetGuildUser(user);

            if (dbGuild is null || dbGuild.QuarantineRoleId == ulong.MinValue)
            {
                await Interaction.ReplyErrorAsync("Quarantine is not enabled in this guild");
                return;
            }
            if (FindRole(dbGuild.VouchRoleId) is null)
            {
                await Interaction.ReplyDeletedRoleErrorAsync("Quarantine");
                return;
            }

            if (gTargetUser.FindRole(dbGuild.QuarantineRoleId) is not null)
            {
                _logger.LogDebug("{intTag} Removing quarantine role from user {targetUserData}, has been removed({quarantineRoleId}) in {guild} by {userData}", GetIntTag(), gTargetUser.Log(), dbGuild.QuarantineRoleId, Context.Guild.Log(), gUser.Log());
                await gTargetUser.RemoveRoleAsync(dbGuild.QuarantineRoleId);
                _logger.LogInformation("{intTag} Removed quarantine role from user {targetUserData}, has been removed({quarantineRoleId}) in {guild} by {userData}", GetIntTag(), gTargetUser.Log(), dbGuild.QuarantineRoleId, Context.Guild.Log(), gUser.Log());
                await Interaction.ReplyAsync($"{gTargetUser.Mention} is no longer quarantined");
                return;
            }

            _logger.LogDebug("{intTag} Giving quarantine role to user {targetUserData}, has been quarantined({quarantineRoleId}) in {guild} by {userData}", GetIntTag(), gTargetUser.Log(), dbGuild.QuarantineRoleId, Context.Guild.Log(), gUser.Log());
            await gTargetUser.AddRoleAsync(dbGuild.QuarantineRoleId);
            _logger.LogInformation("{intTag} Gave quarantine role to user {targetUserData}, has been quarantined({quarantineRoleId}) in {guild} by {userData}", GetIntTag(), gTargetUser.Log(), dbGuild.QuarantineRoleId, Context.Guild.Log(), gUser.Log());
            await Interaction.ReplyAsync($"{gTargetUser.Mention} has been quarantined");
        }
    }
}
