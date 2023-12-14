using Discord;
using Discord.WebSocket;
using SolarisBot.Discord.Common;

namespace SolarisBot.Discord.Modules.UserAnalysis
{
    internal class UserAnalysis
    {
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
        internal ulong UserBadges { get; private set; } = 0;
        //All code regarding accessing a users online state has been disabled as the bot does not have access to this info
        //internal UserAnalysisOnlineState OnlineState {  get; private set; } = UserAnalysisOnlineState.Online;

        private const UserProperties _userBadgeFlags = UserProperties.Staff | UserProperties.Partner | UserProperties.HypeSquadEvents | UserProperties.BugHunterLevel1
            | UserProperties.HypeSquadBalance | UserProperties.HypeSquadBravery | UserProperties.HypeSquadBrilliance | UserProperties.EarlySupporter | UserProperties.BugHunterLevel2
            | UserProperties.EarlyVerifiedBotDeveloper | UserProperties.DiscordCertifiedModerator | UserProperties.ActiveDeveloper; //All important badges as a flag for AND with user flags
        private const int _failedOldDiscriminatorCheckPenalty = 30;
        private const int _failedDefaultPfpCheckPenalty = 75;
        private const int _noBadgesPenalty = 30;
        private const int _badgeValue = -15;
        //private const int _userOfflinePenalty = 50;
        //private const int _userInvisiblePenalty = 15;

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

            var failedDiscriminatorCheck = user.DiscriminatorValue == 0;
            var failedProfileCheck = user.GetAvatarUrl() == null; //todo: does this check function?

            ulong userBadges = 0;
            if (user.PublicFlags.HasValue)
            {
                var flags = user.PublicFlags.Value & _userBadgeFlags;
                userBadges = ulong.PopCount((ulong)flags);
            }
            var badgeValue = userBadges == 0 ? 30 : Convert.ToInt32(userBadges) * -15;

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
                UserBadges = userBadges,
                //OnlineState = onlineState,
            };
        }

        internal int CalculateScore()
        {
            var score = CalculateRuleScoreSum(FailedKeywordRulesUsername) + CalculateRuleScoreSum(FailedKeywordRulesGlobalname);

            if (FailedTimeRule is not null)
                score += FailedTimeRule.Score;
            if (FailedOldDiscriminatorCheck)
                score += _failedOldDiscriminatorCheckPenalty;
            if (FailedDefaultPfpCheck)
                score += _failedDefaultPfpCheckPenalty;

            score += UserBadges == 0
                ? _noBadgesPenalty
                : Convert.ToInt32(UserBadges) * _badgeValue;

            //if (OnlineState != UserAnalysisOnlineState.Online)
            //    score += OnlineState == UserAnalysisOnlineState.Invisible
            //        ? _userInvisiblePenalty
            //        : _userOfflinePenalty;

            return score;
        }

        internal Embed GenerateSummaryEmbed(int? score = null) //todo: formatting
        {
            var summaryStrings = new List<string>();

            if (FailedKeywordRulesUsername.Count > 0)
                summaryStrings.Add($"Username({CalculateRuleScoreSum(FailedKeywordRulesUsername)}): {string.Join(", ", FailedKeywordRulesUsername)}");
            if (FailedKeywordRulesGlobalname.Count > 0)
                summaryStrings.Add($"Globalname({CalculateRuleScoreSum(FailedKeywordRulesGlobalname)}): {string.Join(", ", FailedKeywordRulesGlobalname)}");
            if (FailedTimeRule is not null)
                summaryStrings.Add($"Joined: {FailedTimeRule}");

            var othersStrings = new List<string>();
            if (FailedOldDiscriminatorCheck)
                othersStrings.Add($"Old discriminator({_failedOldDiscriminatorCheckPenalty})");
            if (FailedDefaultPfpCheck)
                othersStrings.Add($"No PFP({_failedDefaultPfpCheckPenalty})");
            othersStrings.Add($"{(UserBadges == 0 ? "No " : string.Empty)}Badges({CalculateBadgeScore()})");
            //if (OnlineState != UserAnalysisOnlineState.Online)
            //{
            //    othersStrings.Add(OnlineState == UserAnalysisOnlineState.Invisible
            //        ? $"Invisible({_userInvisiblePenalty})"
            //        : $"Offline({_userOfflinePenalty})"
            //        );
            //}
            summaryStrings.Add($"Other: {string.Join(", ", othersStrings)}");

            score ??= CalculateScore();

            var embed = EmbedFactory.Builder()
                .WithTitle($"Analysis of {_user.DisplayName}({score})")
                .WithThumbnailUrl(_user.GetDisplayAvatarUrl())
                .WithDescription(string.Join("\n", summaryStrings));

            return embed.Build();
        }

        internal int CalculateBadgeScore()
            => UserBadges == 0
            ? _noBadgesPenalty
            : Convert.ToInt32(UserBadges) * _badgeValue;

        internal static int CalculateRuleScoreSum<T>(IEnumerable<T> enumerable) where T : CredibilityRule
            => enumerable.Sum(x => x.Score);
    }

    //internal enum UserAnalysisOnlineState
    //{
    //    Online,
    //    Invisible,
    //    Offline
    //}
}
