using Discord.Interactions;
using Discord;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.UserAnalysis
{
    [Module("cfg-useranalysis"), Group("useranalysis", "[MODERATE MEMBERS ONLY] User analysis commands")]
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

        [SlashCommand("config", "Set up user analysis")]
        public async Task ConfigureAnalysisAsync
        (
            [Summary(description: "Notification channel (none to disable)")] IChannel? channel = null,
            [Summary(description: "[Optional] Minimum points for warning")] int minWarn = int.MaxValue,
            [Summary(description: "[Optional] Minimum points for kick")] int minKick = int.MaxValue,
            [Summary(description: "[Optional] Minimum points for ban")] int minBan = int.MaxValue
        )
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.UserAnalysisChannel = channel?.Id ?? 0;
            guild.UserAnalysisWarnAt = minWarn;
            guild.UserAnalysisKickAt = minKick;
            guild.UserAnalysisBanAt = minBan;

            _logger.LogDebug("{intTag} Setting userAnalysis to channel={analysisChannel}, minWarn={minWarn}, minKick={minKick}, minBan={minBan} in guild {guild}", GetIntTag(), channel?.Log() ?? "0", minWarn, minKick, minBan, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set userAnalysis to channel={analysisChannel}, minWarn={minWarn}, minKick={minKick}, minBan={minBan} in guild {guild}", GetIntTag(), channel?.Log() ?? "0", minWarn, minKick, minBan, Context.Guild.Log());
            await Interaction.ReplyAsync($"User analysis is currently **{(channel is not null ? "enabled" : "disabled")}**\n\nChannel: **{(channel is null ? "None" : $"<#{channel.Id}>")}**\nWarn at: **{(minWarn == int.MaxValue ? "OFF" : minWarn)}**\nKick at: **{(minKick == int.MaxValue ? "OFF" : minKick)}**\nBan at: **{(minBan == int.MaxValue ? "OFF" : minBan)}**");
        }

        [UserCommand("Analyze"), SlashCommand("analyze", "Analyze a user")]
        public async Task AnalyzeUserAsync(IUser user)
        {
            if (user.IsBot || user.IsWebhook)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }
            var gUser = GetGuildUser(user);
            var embed = UserAnalysis.ForUser(gUser, _config).GenerateSummaryEmbed();
            await Interaction.ReplyAsync(embed);
        }

        [ComponentInteraction("solaris_analysis_kick.*", true), RequireBotPermission(GuildPermission.KickMembers)]
        public async Task HandleButtonAnalysisKickAsync(string userId)
            => await ModerateUserAsync(userId, false);

        [ComponentInteraction("solaris_analysis_ban.*", true), RequireBotPermission(GuildPermission.BanMembers)]
        public async Task HandleButtonAnalysisBanAsync(string userId)
            => await ModerateUserAsync(userId, true);

        private async Task ModerateUserAsync(string userId, bool ban)
        {
            var gUser = GetGuildUser(Context.User);
            if ((!ban && !gUser.GuildPermissions.KickMembers) || (ban && !gUser.GuildPermissions.BanMembers))
            {
                await Interaction.ReplyErrorAsync($"You do not have permission to {(ban ? "ban" : "kick")} members");
                return;
            }

            var targetUser = await Context.Guild.GetUserAsync(ulong.Parse(userId));
            if (targetUser is null)
            {
                await Interaction.ReplyErrorAsync("User could not be found");
                return;
            }

            var verb = ban ? "Bann" : "Kick";
            _logger.LogDebug("{verb}ing user {targetUser} from guild {guild} via analysis result button triggered by {user}", verb, targetUser.Log(), Context.Guild.Log(), Context.User.Log());
            if (ban)
                await targetUser.BanAsync(reason: $"Banned by {Context.User.Log()} via analysis result button");
            else
                await targetUser.KickAsync($"Kicked by {Context.User.Log()} via analysis result button");
            _logger.LogInformation("{verb}ed user {targetUser} from guild {guild} via analysis result button triggered by {user}", verb, targetUser.Log(), Context.Guild.Log(), Context.User.Log());
            await Interaction.ReplyAsync($"User has been {verb.ToLower()}ed");
        }
    }
}
