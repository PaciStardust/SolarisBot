using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Discord.Commands
{
    [RequireContext(ContextType.Guild)]
    public sealed class GrouplessCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<GrouplessCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal GrouplessCommands(ILogger<GrouplessCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }
        protected override ILogger? GetLogger() => _logger;

        [SlashCommand("vouch", "Vouch for a user")]
        public async Task VouchUserAsync(IUser user)
        {
            var dbGuild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);

            if (Context.User is not SocketGuildUser gUser || user is not SocketGuildUser gTargetUser || dbGuild == null //Not a guild or no dbguild
                || dbGuild.VouchPermissionRoleId == 0 || dbGuild.VouchRoleId == 0 //Vouching not set up
                || gUser.Roles.FirstOrDefault(x => x.Id == dbGuild.VouchPermissionRoleId) == null) //User can vouch
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden, isEphemeral: true);
                return;
            }

            if (gTargetUser.Roles.FirstOrDefault(x => x.Id == dbGuild.VouchRoleId) != null)
            {
                await RespondEmbedAsync("Already Vouched", $"{gTargetUser.Mention} has already been vouched", isEphemeral: true);
                return;
            }

            try
            {
                await gTargetUser.AddRoleAsync(dbGuild.VouchRoleId);
                _logger.LogInformation("User {targetUserName}({targetUserId}) has been vouched({vouchRoleId}) for in {guildId} by {userName}({userId})", gTargetUser.Username, gTargetUser.Id, dbGuild.VouchRoleId, Context.Guild.Id, gUser.Username, gUser.Id);
                await RespondEmbedAsync("Vouch Successful", $"Vouched for {gTargetUser.Mention}, welcome to the server!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "User {targetUserName}({targetUserId}) has failed to be vouched({vouchRoleId}) for in {guildId} by {userName}({userId})", gTargetUser.Username, gTargetUser.Id, dbGuild.VouchRoleId, Context.Guild.Id, gUser.Username, gUser.Id);
                await RespondErrorEmbedAsync(ex, isEphemeral: true);
            }
        }
    }
}
