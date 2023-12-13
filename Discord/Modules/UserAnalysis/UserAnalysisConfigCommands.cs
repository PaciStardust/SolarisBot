using Discord.Interactions;
using Discord;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.UserAnalysis
{
    [Module("useranalysis"), Group("useranalysis", "[MODERATE MEMBERS ONLY] User analysis commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ModerateMembers), RequireUserPermission(GuildPermission.ModerateMembers)]
    internal class UserAnalysisConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<UserAnalysisConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        private readonly BotConfig _config;
        internal UserAnalysisConfigCommands(ILogger<UserAnalysisConfigCommands> logger, DatabaseContext dbctx, BotConfig config)
        {
            _dbContext = dbctx;
            _logger = logger;
            _config = config;
        }

        [SlashCommand("config", "Set up user analysis")] //todo: [TESTING] Does configure work?
        public async Task ConfigureAnalysisAsynx
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

        [UserCommand("Analyze"), SlashCommand("analyze", "Analyze a user")]
        public async Task AnalyzeUserAsync(IUser user) //todo: does analysis work?
        {
            var gUser = GetGuildUser(user);
            var embed = UserAnalysis.ForUser(gUser, _config).GenerateSummaryEmbed();
            await Interaction.ReplyAsync(embed);
        }
    }
}
