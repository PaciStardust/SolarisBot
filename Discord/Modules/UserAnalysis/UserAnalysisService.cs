using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.UserAnalysis
{
    [Module("useranalysis"), AutoLoadService]
    internal class UserAnalysisService
    {
        private readonly ILogger<UserAnalysisService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly BotConfig _config;
        private readonly DatabaseService _dbService;

        public UserAnalysisService(ILogger<UserAnalysisService> logger, DiscordSocketClient client, BotConfig config, DatabaseService dbService)
        {
            _logger = logger;
            _client = client;
            _config = config;
            _dbService = dbService;

            _client.UserJoined += EvaluateUserCredibilityAsync;
        }

        #region Commands
        /// <summary>
        /// Configures user analasys in a guild
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="channel">Channel for notifications</param>
        /// <param name="minWarn">Minimum points for automatic warn</param>
        /// <param name="minKick">Minimum points for automatic kick</param>
        /// <param name="minBan">Minimum points for automatic ban</param>
        /// <returns>GuildConfig on success / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigUserAnalysisAsync(IGuild guild, IChannel? channel, int minWarn, int minKick, int minBan)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);

            dbGuild.UserAnalysisChannelId = channel?.Id ?? ulong.MinValue;
            dbGuild.UserAnalysisWarnAt = minWarn;
            dbGuild.UserAnalysisKickAt = minKick;
            dbGuild.UserAnalysisBanAt = minBan;

            _logger.LogTrace("Setting userAnalysis to channel={analysisChannel}, minWarn={minWarn}, minKick={minKick}, minBan={minBan} in guild {guild}", channel?.Log() ?? "0", minWarn, minKick, minBan, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting userAnalysis to channel={analysisChannel}, minWarn={minWarn}, minKick={minKick}, minBan={minBan} in guild {guild}", channel?.Log() ?? "0", minWarn, minKick, minBan, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogDebug("Set userAnalysis to channel={analysisChannel}, minWarn={minWarn}, minKick={minKick}, minBan={minBan} in guild {guild}", channel?.Log() ?? "0", minWarn, minKick, minBan, guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }

        /// <summary>
        /// Analyzes a user
        /// </summary>
        /// <param name="user">User to analyze</param>
        /// <returns>Analysis on success / Error string</returns>
        internal OneOf<Success<UserAnalysis>, Error<string>> AnalyzeUser(IUser user)
        {
            if (user.IsBot || user.IsWebhook)
                return new Error<string>(StandardError.NoResults);

            if (user is not SocketGuildUser gUser)
                return new Error<string>(StandardError.FailedConversion("target user", "SocketGuildUser"));

            var analysis = UserAnalysis.ForUser(gUser, _config);
            return new Success<UserAnalysis>(analysis);
        }

        /// <summary>
        /// Kicks or bans a user from a guild
        /// </summary>
        /// <param name="guild">Guild to ban from</param>
        /// <param name="executingUser">User executing moderation</param>
        /// <param name="targetUserId">Id of user being targeted</param>
        /// <param name="ban">Should the action be a ban?</param>
        /// <returns>Success / Error string / Exception</returns>
        internal async Task<OneOf<Success, Error<string>, Error<Exception>>> ModerateUserAsync(IGuild guild, IUser executingUser, ulong targetUserId, bool ban)
        {
            if (executingUser is not SocketGuildUser executingGuildUser)
                return new Error<string>(StandardError.FailedConversion("executing user", "SocketGuildUser"));

            if ((!ban && !executingGuildUser.GuildPermissions.KickMembers) || (ban && !executingGuildUser.GuildPermissions.BanMembers))
                return new Error<string>($"You do not have permission to {(ban ? "ban" : "kick")} members");

            var targetGuildUser = await guild.GetUserAsync(targetUserId);
            if (targetGuildUser is null)
                return new Error<string>(StandardError.NoResults);

            var verb = ban ? "Bann" : "Kick";
            try
            {
                _logger.LogTrace("{verb}ing user {targetUser} from guild {guild} via analysis result button triggered by {user}", verb, targetGuildUser.Log(), guild.Log(), executingGuildUser.Log());
                if (ban)
                    await targetGuildUser.BanAsync(reason: $"Banned by {executingGuildUser.Log()} via analysis result button");
                else
                    await targetGuildUser.KickAsync($"Kicked by {executingGuildUser.Log()} via analysis result button");
                _logger.LogDebug("{verb}ed user {targetUser} from guild {guild} via analysis result button triggered by {user}", verb, targetGuildUser.Log(), targetGuildUser.Log(), executingGuildUser.Log());
                return new Success();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed {verb}ing user {targetUser} from guild {guild} via analysis result button triggered by {user}", verb.ToLower(), targetGuildUser.Log(), guild.Log(), executingGuildUser.Log());
                return new Error<Exception>(ex);
            }
        }
        #endregion

        #region Join Handling
        /// <summary>
        /// Evaluates the credibility of a user and may automatically moderate them
        /// </summary>
        /// <param name="user">User to evaluate</param>
        private async Task EvaluateUserCredibilityAsync(SocketGuildUser user)
        {
            if (user.IsWebhook || user.IsBot)
                return;

            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetGuildByIdAsync(user.Guild.Id);
            if (dbGuild is null || dbGuild.UserAnalysisChannelId == ulong.MinValue)
                return;

            var analysis = UserAnalysis.ForUser(user, _config);

            var channel = await _client.GetChannelAsync(dbGuild.UserAnalysisChannelId);
            if (channel is null || channel is not IMessageChannel msgChannel)
            {
                _logger.LogDebug("Could not locate UserAnalysisChannel for guild {guild} with id {channelId}", dbGuild, dbGuild.UserAnalysisChannelId);
                return;
            }

            var analysisScore = analysis.CalculateScore();
            var (actionText, completedAction) = await AutomaticallyModerateUser(user, analysisScore, dbGuild);

            var componentBuilder = new ComponentBuilder();
            if (completedAction < ModerationAction.Kick)
            {
                componentBuilder = componentBuilder
                    .WithButton("Kick", $"solaris_analysis_kick.{user.Id}", ButtonStyle.Danger, disabled: !user.Guild.CurrentUser.GuildPermissions.KickMembers)
                    .WithButton("Ban", $"solaris_analysis_ban.{user.Id}", ButtonStyle.Danger, disabled: !user.Guild.CurrentUser.GuildPermissions.BanMembers);
            }

            actionText = $"{(completedAction >= ModerationAction.Warn ? "@here " : string.Empty)}{user.Mention} " + actionText;
            try
            {
                _logger.LogTrace("Sending user analysis {analyis} to channel {channel}", analysis.Log(analysisScore), channel.Log());
                await msgChannel.SendMessageAsync(actionText, embed: analysis.GenerateSummaryEmbed(analysisScore), components: componentBuilder.Build());
                _logger.LogDebug("Sent user analysis {analyis} to channel {channel}", analysis.Log(analysisScore), channel.Log());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed sending user analysis {analyis} to channel {channel}", analysis.Log(analysisScore), channel.Log());
            }
        }

        /// <summary>
        /// Automatically moderates a user based on score
        /// </summary>
        /// <param name="targetUser">User to moderate</param>
        /// <param name="analysisScore">Score of user</param>
        /// <param name="dbGuild">Guild for action</param>
        /// <returns>Response and Action Taken</returns>
        private async Task<(string, ModerationAction)> AutomaticallyModerateUser(SocketGuildUser targetUser, int analysisScore, DbGuildConfig dbGuild)
        {
            //Establishing needed action
            var moderationAction = ModerationAction.None;
            if (analysisScore >= dbGuild.UserAnalysisWarnAt)
                moderationAction = ModerationAction.Warn;
            if (analysisScore >= dbGuild.UserAnalysisKickAt)
                moderationAction = ModerationAction.Kick;
            if (analysisScore >= dbGuild.UserAnalysisBanAt)
                moderationAction = ModerationAction.Ban;

            switch(moderationAction)
            {
                case ModerationAction.None:
                    return ("appears legit", ModerationAction.None);

                case ModerationAction.Warn:
                    return ($"was classified as suspicious *({analysisScore} >= {dbGuild.UserAnalysisWarnAt} Score)*", ModerationAction.Warn);

                case ModerationAction.Kick:
                    if (!targetUser.Guild.CurrentUser.GuildPermissions.KickMembers)
                        return ("was not kicked as permission is missing", ModerationAction.Warn);
                    try
                    {
                        _logger.LogTrace("Kicking user {user} from guild {guild}, user analysis score {score} satisfies kick score {kickScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisKickAt);
                        await targetUser.KickAsync($"Automatically kicked via user analysis ({analysisScore} >= {dbGuild.UserAnalysisKickAt} Score)");
                        _logger.LogDebug("Kicked user {user} from guild {guild}, user analysis score {score} satisfies kick score {kickScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisKickAt);
                        return ($"was automatically kicked *({analysisScore} >= {dbGuild.UserAnalysisKickAt} Score)*", ModerationAction.Kick);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed kicking user {user} from guild {guild}, user analysis score {score} satisfies kick score {kickScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisKickAt);
                        return ($"was not kicked *({ex.Message})*", ModerationAction.Warn);
                    }

                case ModerationAction.Ban:
                    if (!targetUser.Guild.CurrentUser.GuildPermissions.BanMembers)
                        return ("was not banned as permission is missing", ModerationAction.Warn);
                    try
                    {
                        _logger.LogTrace("Banning user {user} from guild {guild}, user analysis score {score} satisfies ban score {banScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisBanAt);
                        await targetUser.KickAsync($"Automatically banned via user analysis ({analysisScore} >= {dbGuild.UserAnalysisBanAt} Score)");
                        _logger.LogDebug("Banned user {user} from guild {guild}, user analysis score {score} satisfies ban score {banScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisBanAt);
                        return ($"was automatically banned *({analysisScore} >= {dbGuild.UserAnalysisBanAt} Score)*", ModerationAction.Ban);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed banned user {user} from guild {guild}, user analysis score {score} satisfies ban score {banScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisBanAt);
                        return ($"was not banned *({ex.Message})*", ModerationAction.Warn);
                    }

                default:
                    return ("received unknown evaluation*", ModerationAction.Warn);
            }
        }

        private enum ModerationAction
        {
            None,
            Warn,
            Kick,
            Ban
        }
        #endregion
    }
}
