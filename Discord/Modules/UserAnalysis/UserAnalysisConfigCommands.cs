using Discord.Interactions;
using Discord;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.UserAnalysis
{
    [Module("useranalysis")]
    internal class UserAnalysisConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<UserAnalysisConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal UserAnalysisConfigCommands(ILogger<UserAnalysisConfigCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("cfg-useranalysis", "[MODERATE MEMBERS ONLY] Set up user analysis"), RequireUserPermission(GuildPermission.ModerateMembers)] //todo: [TESTING] Does configure work?
        public async Task ConfigureGifify
        (
            [Summary(description: "Notification channel (none to disable)")] IChannel? channel, //todo: tweak defaults,is param optional
            [Summary(description: "Minimum points for warning")] ulong minWarn = 0,
            [Summary(description: "Minimum points for ban")] ulong minBan = 0
        )
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.UserAnalysisChannel = channel?.Id ?? 0;
            guild.UserAnalysisWarnAt = minWarn;
            guild.UserAnalysisBanAt = minBan;

            _logger.LogDebug("{intTag} Setting userAnalysis to channel={analysisChannel}, minWarn={minWarn}, minBan={minBan} in guild {guild}", GetIntTag(), channel?.Log() ?? "0", minWarn, minBan, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set userAnalysis to channel={analysisChannel}, minWarn={minWarn}, minBan={minBan} in guild {guild}", GetIntTag(), channel?.Log() ?? "0", minWarn, minBan, Context.Guild.Log());
            await Interaction.ReplyAsync($"User analysis is currently **{(channel is not null ? "enabled" : "disabled")}**\n\nChannel: **{(channel is null ? "None" : $"<#{channel.Id}>")}**\nWarn at: **{minWarn}**\nBan at: **{minBan}**");
        }
    }
}
