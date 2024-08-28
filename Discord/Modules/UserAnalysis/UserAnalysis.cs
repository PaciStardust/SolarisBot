using Discord;
using Discord.WebSocket;
using SolarisBot.Discord.Common;

namespace SolarisBot.Discord.Modules.UserAnalysis
{
    internal class UserAnalysis
    {
        //All code regarding accessing a users online state has been disabled as the bot does not have access to this info
        //internal UserAnalysisOnlineState OnlineState {  get; private set; } = UserAnalysisOnlineState.Online;

        private const UserProperties _userBadgeFlags = UserProperties.Staff | UserProperties.Partner | UserProperties.HypeSquadEvents | UserProperties.BugHunterLevel1
            | UserProperties.HypeSquadBalance | UserProperties.HypeSquadBravery | UserProperties.HypeSquadBrilliance | UserProperties.EarlySupporter | UserProperties.BugHunterLevel2
            | UserProperties.EarlyVerifiedBotDeveloper | UserProperties.DiscordCertifiedModerator | UserProperties.ActiveDeveloper; //All important badges as a flag for AND with user flags

        private const int _failedOldDiscriminatorCheckPenalty = 20;
        private const int _failedDefaultPfpCheckPenalty = 75;
        private const int _noBadgesPenalty = 25;
        //private const int _userOfflinePenalty = 50;
        //private const int _userInvisiblePenalty = 15;
        private const int _rejoinPenalty = 0;

        private const int _badgeBonus = -20;
        private const int _decorationBonus = -10;
        private const int _mutualBonus = -10;

        private readonly SocketGuildUser _user;

        private UserAnalysis(SocketGuildUser user)
        {
            _user = user;
        }

        internal IReadOnlyList<KeywordCredibilityRule> FailedKeywordRulesUsername { get; private set; } = new List<KeywordCredibilityRule>();
        internal IReadOnlyList<KeywordCredibilityRule> FailedKeywordRulesGlobalname { get; private set; } = new List<KeywordCredibilityRule>();
        internal TimeCredibilityRule? FailedTimeRule { get; private set; } = null;
        internal bool FailedOldDiscriminatorCheck { get; private set; } = false;
        internal bool FailedDefaultPfpCheck { get; private set; } = false;
        internal bool HasRejoined { get; private set; } = false;
        internal bool HasDecoration { get; private set; } = false;
        internal ulong UserBadges { get; private set; } = 0;
        internal int MutualGuilds { get; private set; } = 0;

        /// <summary>
        /// Does user analysis on a user
        /// </summary>
        /// <param name="user">User to analyze</param>
        /// <param name="config">Config to use</param>
        /// <returns>Analysis of user</returns>
        internal static UserAnalysis ForUser(SocketGuildUser user, BotConfig config)
        {
            var failedUsernameChecks = new List<KeywordCredibilityRule>();
            var failedGlobalnameChecks = new List<KeywordCredibilityRule>();
            foreach (var rule in config.CredibilityRulesKeyword)
            {
                if (!rule.IsCredible(user.Username))
                    failedUsernameChecks.Add(rule);
                if (!rule.IsCredible(user.GlobalName))
                    failedGlobalnameChecks.Add(rule);
            }

            TimeCredibilityRule? failedTimeCheck = null;
            if (user.JoinedAt is not null)
            {
                var timeDiff = user.JoinedAt - user.CreatedAt;
                foreach (var rule in config.CredibilityRulesTime.OrderBy(x => x.MinimumAge))
                {
                    if (!rule.IsCredible(timeDiff.Value))
                    {
                        failedTimeCheck = rule;
                        break;
                    }
                }
            }

            var failedDiscriminatorCheck = user.DiscriminatorValue != 0;
            var failedProfileCheck = user.GetAvatarUrl() == null;

            var hasDecoration = user.AvatarDecorationHash is not null; //todo: impl
            var mutualGuildCount = user.MutualGuilds.Count; //todo: impl
            var rejoined = user.Flags.HasFlag(GuildUserFlags.DidRejoin); //todo: impl

            ulong userBadges = 0;
            if (user.PublicFlags.HasValue)
            {
                
                var flags = user.PublicFlags.Value & _userBadgeFlags;
                userBadges = ulong.PopCount((ulong)flags);
            }
            var badgeValue = userBadges == 0 ? 30 : Convert.ToInt32(userBadges) * _badgeBonus;

            //var onlineState = user.Status.HasFlag(UserStatus.Offline)
            //    ? UserAnalysisOnlineState.Offline
            //    : user.Status.HasFlag(UserStatus.Invisible)
            //    ? UserAnalysisOnlineState.Invisible
            //    : UserAnalysisOnlineState.Online;



            return new UserAnalysis(user)
            {
                FailedKeywordRulesUsername = failedUsernameChecks,
                FailedKeywordRulesGlobalname = failedGlobalnameChecks,
                FailedTimeRule = failedTimeCheck,
                FailedOldDiscriminatorCheck = failedDiscriminatorCheck,
                FailedDefaultPfpCheck = failedProfileCheck,
                HasDecoration = hasDecoration,
                MutualGuilds = mutualGuildCount,
                HasRejoined = rejoined,
                UserBadges = userBadges,
                //OnlineState = onlineState,
            };
        }

        /// <summary>
        /// Calculates the credibility score
        /// </summary>
        /// <returns>Calculated score</returns>
        internal int CalculateScore()
        {
            var score = CalculateRuleScoreSum(FailedKeywordRulesUsername) + CalculateRuleScoreSum(FailedKeywordRulesGlobalname);

            if (FailedTimeRule is not null)
                score += FailedTimeRule.Score;
            if (FailedOldDiscriminatorCheck)
                score += _failedOldDiscriminatorCheckPenalty;
            if (FailedDefaultPfpCheck)
                score += _failedDefaultPfpCheckPenalty;
            if (MutualGuilds > 1)
                score += _mutualBonus;
            if (HasDecoration)
                score += _decorationBonus;
            if (HasRejoined)
                score += _rejoinPenalty;

            score += UserBadges == 0
                ? _noBadgesPenalty
                : Convert.ToInt32(UserBadges) * _badgeBonus;

            //if (OnlineState != UserAnalysisOnlineState.Online)
            //    score += OnlineState == UserAnalysisOnlineState.Invisible
            //        ? _userInvisiblePenalty
            //        : _userOfflinePenalty;

            return score;
        }

        /// <summary>
        /// Generates an embed summarizing the analysis
        /// </summary>
        /// <param name="score">Score if already calculated to avoid double calc</param>
        /// <returns>Generated embed</returns>
        internal Embed GenerateSummaryEmbed(int? score = null)
        {
            var summaryStrings = new List<string>();

            if (FailedKeywordRulesUsername.Count > 0)
                summaryStrings.Add($"**Username**:\n{string.Join(", ", FailedKeywordRulesUsername)}");
            if (FailedKeywordRulesGlobalname.Count > 0)
                summaryStrings.Add($"**Globalname**:\n{string.Join(", ", FailedKeywordRulesGlobalname)}");
            if (FailedTimeRule is not null)
                summaryStrings.Add($"**Joined**:\n{FailedTimeRule}");

            var othersStrings = new List<string>();
            if (FailedOldDiscriminatorCheck)
                othersStrings.Add($"Old discriminator *({_failedOldDiscriminatorCheckPenalty})*");
            if (FailedDefaultPfpCheck)
                othersStrings.Add($"No PFP *({_failedDefaultPfpCheckPenalty})*");
            othersStrings.Add($"{(UserBadges == 0 ? "No " : string.Empty)}Badges *({CalculateBadgeScore()})*");
            if (MutualGuilds > 1)
                othersStrings.Add($"Has multiple mutual guilds *({_mutualBonus})*");
            if (HasDecoration)
                othersStrings.Add($"Has decoration *({_decorationBonus})*");
            if (HasRejoined)
                othersStrings.Add($"Has rejoined *({_rejoinPenalty})*");
            if (othersStrings.Count > 0)
                summaryStrings.Add($"**Other**:\n{string.Join(", ", othersStrings)}");

            if (summaryStrings.Count == 0)
                summaryStrings.Add("Nothing to report");

            score ??= CalculateScore();

            var embed = EmbedFactory.Builder()
                .WithTitle($"Analysis of {_user.DisplayName} ({score} Score)")
                .WithThumbnailUrl(_user.GetAvatarUrl())
                .WithDescription(string.Join("\n\n", summaryStrings))
                .WithFooter("Lower score is better");

            return embed.Build();
        }

        /// <summary>
        /// Calculates the score from badges
        /// </summary>
        /// <returns>Calculated score</returns>
        internal int CalculateBadgeScore()
            => UserBadges == 0
            ? _noBadgesPenalty
            : Convert.ToInt32(UserBadges) * _badgeBonus;

        internal static int CalculateRuleScoreSum<T>(IEnumerable<T> enumerable) where T : CredibilityRule
            => enumerable.Sum(x => x.Score);

        internal string Log(int? score = null)
            => $"{_user.Log()} => {score ?? CalculateScore()}";
    }

    //internal enum UserAnalysisOnlineState
    //{
    //    Online,
    //    Invisible,
    //    Offline
    //}
}
