using Discord.Interactions;
using Discord;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Discord.Modules.Fun
{
    [Module("fun/regex"), Group("cfg-regex", "[MANAGE CHANNELS ONLY] Bridge config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageChannels), RequireUserPermission(GuildPermission.ManageChannels)]
    public sealed class RegexChannelConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<RegexChannelConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal RegexChannelConfigCommands(ILogger<RegexChannelConfigCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        //todo: [FEATURE] list, service, cleanup

        public async Task ConfigureRegexChannel //todo: [TEST] Test regex channel config
        (
            [Summary(description: "[Opt] Target channel")] IChannel? channel = null,
            [Summary(description: "[Opt] Regex to enforce (None to disable)")] string regex = "",
            [Summary(description: "[Opt] Role to apply as punishment")] IRole? punishmentRole = null,
            [Summary(description: "[Opt] Message to send on fail")] string punishmentMsg = "",
            [Summary(description: "[Opt] Delete fail message")] bool deleteMsg = false
        ) 
        {
            var thisChannel = channel ?? Context.Channel;
            if (string.IsNullOrWhiteSpace(regex))
            {
                var deleteChannel = await _dbContext.RegexChannels.FirstOrDefaultAsync(x => x.ChannelId == thisChannel.Id);
                if (deleteChannel is null)
                {
                    await Interaction.ReplyErrorAsync($"Failed to find a RegEx config for channel id {thisChannel.Id}");
                    return;
                }
                _dbContext.RegexChannels.Remove(deleteChannel);
                _logger.LogDebug("{intTag} Deleting regex {deleteRegex} for channel {channel} in guild {guild}", GetIntTag(), deleteChannel, thisChannel.Log(), Context.Guild.Log());
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("{intTag} Deleted regex {deleteRegex} for channel {channel} in guild {guild}", GetIntTag(), deleteChannel, thisChannel.Log(), Context.Guild.Log());
                await Interaction.ReplyAsync($"Deleted RegEx **\"{deleteChannel}\"** for channel **<#{thisChannel.Id}>**");
                return;
            }

            try
            {
                _ = new Regex(regex);
            }
            catch
            {
                await Interaction.ReplyErrorAsync($"Failed to validate RegEx: {regex}");
                return;
            }

            var dbGuild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id, x => x.Include(y => y.RegexChannels));
            var dbChannel = dbGuild.RegexChannels.FirstOrDefault(x => x.ChannelId == thisChannel.Id)
                ?? new DbRegexChannel() { GuildId = Context.Guild.Id, ChannelId = thisChannel.Id };

            dbChannel.Regex = regex;
            dbChannel.AppliedRoleId = punishmentRole?.Id ?? ulong.MinValue;
            dbChannel.PunishmentMessage = punishmentMsg;
            dbChannel.PunishmentDelete = deleteMsg;

            _dbContext.RegexChannels.Update(dbChannel);
            _logger.LogDebug("{intTag} Setting regex to rx={channelRegex}, role={punishmentRole}, msg={punishmentMsg}, del={delete} for channel {channel} in guild {guild}", GetIntTag(), dbChannel.Regex, dbChannel.AppliedRoleId, dbChannel.PunishmentMessage, dbChannel.PunishmentDelete, thisChannel.Log(), Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set regex to rx={channelRegex}, role={punishmentRole}, msg={punishmentMsg}, del={delete} for channel {channel} in guild {guild}", GetIntTag(), dbChannel.Regex, dbChannel.AppliedRoleId, dbChannel.PunishmentMessage, dbChannel.PunishmentDelete, thisChannel.Log(), Context.Guild.Log());
            await Interaction.ReplyAsync($"RegEx for **<#{dbChannel.ChannelId}>** created\n\nRegex: **{regex}**\nRole: **{(dbChannel.AppliedRoleId == ulong.MinValue ? "None" : $"{dbChannel.AppliedRoleId}>")}**\nMessage: **{(string.IsNullOrWhiteSpace(dbChannel.PunishmentMessage) ? "None" : $"\"{dbChannel.PunishmentMessage}\"")}**\nDelete: **{(dbChannel.PunishmentDelete ? "Yes" : "No")}**");
        }
    }
}
