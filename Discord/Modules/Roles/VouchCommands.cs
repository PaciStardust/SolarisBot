using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles
{
    [Module("roles/vouch")]
    public sealed class VouchCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<VouchCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal VouchCommands(ILogger<VouchCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("cfg-vouch", "[MANAGE ROLES ONLY] Set up vouching"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task ConfigVouchingAsync
        (
            [Summary(description: "[Opt] Role required for vouching (none to disable)")] IRole? permission = null,
            [Summary(description: "[Opt] Role aquired through vouching (none to disable)")] IRole? vouch = null
        )
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.VouchPermissionRoleId = permission?.Id ?? ulong.MinValue;
            guild.VouchRoleId = vouch?.Id ?? ulong.MinValue;

            _logger.LogDebug("{intTag} Setting vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", GetIntTag(), permission?.Log() ?? "0", vouch?.Log() ?? "0", Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", GetIntTag(), permission?.Log() ?? "0", vouch?.Log() ?? "0", Context.Guild.Log());
            await Interaction.ReplyAsync($"Vouching is currently **{(permission is not null && vouch is not null ? "enabled" : "disabled")}**\n\nPermission: **{permission?.Mention ?? "None"}**\nVouch: **{vouch?.Mention ?? "None"}**");
        }

        [UserCommand("Vouch"), SlashCommand("vouch", "Vouch for a user"), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task VouchUserAsync(IUser user)
        {
            var dbGuild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
            var gUser = GetGuildUser(Context.User);
            var gTargetUser = GetGuildUser(user);

            if (dbGuild is null || !dbGuild.VouchingOn)
            {
                await Interaction.ReplyErrorAsync("Vouching is not enabled in this guild");
                return;
            }

            if (FindRole(dbGuild.VouchPermissionRoleId) is null) //todo: logging?
            {
                await Interaction.ReplyErrorAsync("Vouch permission role could not be found in guild, it might have been deleted");
                return;
            }
            if (gUser.Roles.FirstOrDefault(x => x.Id == dbGuild.VouchPermissionRoleId) is null)
            {
                await Interaction.ReplyErrorAsync($"You do not have the required role <@&{dbGuild.VouchPermissionRoleId}>");
                return;
            }
            if (FindRole(dbGuild.VouchRoleId) is null)
            {
                await Interaction.ReplyErrorAsync("Vouch role could not be found in guild, it might have been deleted");
                return;
            }
            if (gTargetUser.Roles.FirstOrDefault(x => x.Id == dbGuild.VouchRoleId) is not null)
            {
                await Interaction.ReplyErrorAsync($"{gTargetUser.Mention} has already been vouched");
                return;
            }

            _logger.LogDebug("{intTag} Giving vouch role to user {targetUserData}, has been vouched({vouchRoleId}) for in {guild} by {userData}", GetIntTag(), gTargetUser.Log(), dbGuild.VouchRoleId, Context.Guild.Log(), gUser.Log());
            await gTargetUser.AddRoleAsync(dbGuild.VouchRoleId);
            _logger.LogInformation("{intTag} Gave vouch role to user {targetUserData}, has been vouched({vouchRoleId}) for in {guild} by {userData}", GetIntTag(), gTargetUser.Log(), dbGuild.VouchRoleId, Context.Guild.Log(), gUser.Log());
            await Interaction.ReplyAsync($"Vouched for {gTargetUser.Mention}, welcome to the server!");
        }
    }
}
