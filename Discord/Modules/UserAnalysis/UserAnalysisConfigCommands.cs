using Discord.Interactions;
using Discord;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.UserAnalysis
{
    [Module("cfg-useranalysis"), Group("useranalysis", "[MODERATE MEMBERS ONLY] User analysis commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ModerateMembers), RequireUserPermission(GuildPermission.ModerateMembers)]
    internal class UserAnalysisConfigCommands : SolarisInteractionModuleBase
    {
        private readonly UserAnalysisService _userAnalysisService;
        internal UserAnalysisConfigCommands(UserAnalysisService userAnalysisService)
        {
            _userAnalysisService = userAnalysisService;
        }

        [SlashCommand("config", "Set up user analysis")]
        public async Task ConfigureAnalysisAsync
        (
            [Summary(description: "[Opt] Notification channel (none to disable)")] IChannel? channel = null,
            [Summary(description: "[Opt] Minimum points for warning")] int minWarn = int.MaxValue,
            [Summary(description: "[Opt] Minimum points for kick")] int minKick = int.MaxValue,
            [Summary(description: "[Opt] Minimum points for ban")] int minBan = int.MaxValue
        )
        {
            var res = await _userAnalysisService.ConfigUserAnalysisAsync(Context.Guild, channel, minWarn, minKick, minBan);
            await res.Match(
                success => Interaction.ReplyAsync($"User analysis is currently **{(channel is not null ? "enabled" : "disabled")}**\n\nChannel: **{(channel is null ? "None" : $"<#{channel.Id}>")}**\nWarn at: **{(minWarn == int.MaxValue ? "OFF" : minWarn)}**\nKick at: **{(minKick == int.MaxValue ? "OFF" : minKick)}**\nBan at: **{(minBan == int.MaxValue ? "OFF" : minBan)}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [UserCommand("Analyze"), SlashCommand("analyze", "Analyze a user")]
        public async Task AnalyzeUserAsync(IUser user)
        {
            var res = _userAnalysisService.AnalyzeUser(user);
            await res.Match(
                success => Interaction.ReplyAsync(success.Value.GenerateSummaryEmbed()),
                error => Interaction.ReplyErrorAsync(error.Value)
            );
        }

        [ComponentInteraction("solaris_analysis_kick.*", true), RequireBotPermission(GuildPermission.KickMembers)]
        public async Task HandleButtonAnalysisKickAsync(string userId)
            => await ModerateUserAsync(userId, false);

        [ComponentInteraction("solaris_analysis_ban.*", true), RequireBotPermission(GuildPermission.BanMembers)]
        public async Task HandleButtonAnalysisBanAsync(string userId)
            => await ModerateUserAsync(userId, true);

        private async Task ModerateUserAsync(string userId, bool ban)
        {
            if (!ulong.TryParse(userId, out var parsedUserId))
            {
                await Interaction.ReplyInvalidParameterErrorAsync("user ID");
                return;
            }

            var res = await _userAnalysisService.ModerateUserAsync(Context.Guild, Context.User, parsedUserId, ban);
            await res.Match(
                success => Interaction.ReplyAsync($"User has been {(ban ? "ban" : "kick")}ed"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
