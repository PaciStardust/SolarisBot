using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Modules.Roles;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Discord.Modules.UserAnalysis
{
    [Module("useranalysis"), AutoLoadService]
    internal class UserAnalysisService : IHostedService
    {
        private readonly ILogger<UserAnalysisService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseContext _dbCtx;
        private readonly BotConfig _config;

        internal UserAnalysisService(ILogger<UserAnalysisService> logger, DiscordSocketClient client, DatabaseContext dbCtx, BotConfig config)
        {
            _logger = logger;
            _client = client;
            _dbCtx = dbCtx;
            _config = config;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.UserJoined += EvaluateUserCredibilityAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.UserJoined -= EvaluateUserCredibilityAsync;
            return Task.CompletedTask;
        }

        //todo: autoban, warn, ignore at x point level, role at x, kick at x
        //todo: ban button
        //todo: /report command
        //todo: logging

        private static readonly UserProperties _userBadgeFlags = UserProperties.Staff | UserProperties.Partner | UserProperties.HypeSquadEvents | UserProperties.BugHunterLevel1
            | UserProperties.HypeSquadBalance | UserProperties.HypeSquadBravery | UserProperties.HypeSquadBrilliance | UserProperties.EarlySupporter | UserProperties.BugHunterLevel2
            | UserProperties.EarlyVerifiedBotDeveloper | UserProperties.DiscordCertifiedModerator | UserProperties.ActiveDeveloper; //All important badges as a flag for AND with user flags

        private async Task EvaluateUserCredibilityAsync(SocketGuildUser user)
        {
            if (user.IsWebhook || user.IsBot)
                return;

            var dbGuild = await _dbCtx.GetGuildByIdAsync(user.Guild.Id);
            if (dbGuild is null || dbGuild.UserAnalysisChannel == ulong.MinValue)
                return;

            var score = 0;
            var summaryStrings = new List<string>();

            var failedUsernameChecks = new List<KeywordCredibilityRule>();
            var failedGlobalnameChecks = new List<KeywordCredibilityRule>();
            foreach(var rule in _config.CredibilityRulesKeyword)
            {
                if (!rule.IsCredible(user.Username))
                {
                    failedUsernameChecks.Add(rule);
                    score += rule.Score;
                }
                if (!rule.IsCredible(user.GlobalName))
                {
                    failedGlobalnameChecks.Add(rule);
                    score += rule.Score;
                }
            }
            if (failedUsernameChecks.Count > 0)
                summaryStrings.Add($"Username({failedUsernameChecks.Sum(x => x.Score)}): {string.Join(", ", failedUsernameChecks)}");
            if (failedGlobalnameChecks.Count > 0)
                summaryStrings.Add($"Globalname({failedGlobalnameChecks.Sum(x => x.Score)}): {string.Join(", ", failedGlobalnameChecks)}");

            TimeCredibilityRule? failedTimeCheck = null;
            if (user.JoinedAt is not null)
            {
                var timeDiff = user.JoinedAt - user.CreatedAt;
                foreach (var rule in _config.CredibilityRulesTime.OrderBy(x => x.MinimumAge))
                {
                    if (!rule.IsCredible(timeDiff.Value))
                    {
                        failedTimeCheck = rule;
                        score += rule.Score;
                        break;
                    }
                }
            }
            if (failedTimeCheck is not null)
                summaryStrings.Add($"Joined: {failedTimeCheck}");

            var othersStrings = new List<string>();
            var failedDescriminatorCheck = user.DiscriminatorValue == 0;
            if (failedDescriminatorCheck)
            {
                score += 30; //todo: parameterize
                othersStrings.Add("Old name(30)");
            }

            var failedProfileCheck = user.GetAvatarUrl() == null; //todo: does this check function?
            if (failedProfileCheck)
            {
                score += 75; //todo: parameterize
                othersStrings.Add("No PFP(75)");
            }

            ulong userBadges = 0; //todo: does this work???
            if (user.PublicFlags.HasValue)
            {
                var flags = user.PublicFlags.Value & _userBadgeFlags;
                userBadges = ulong.PopCount((ulong)flags);
            }
            var badgeValue = userBadges == 0 ? 30 : Convert.ToInt32(userBadges) * -15;
            score += badgeValue; //todo: parameterize
            othersStrings.Add($"{(userBadges == 0 ? "No " : string.Empty)}Badges({badgeValue})");

            var setOffline = user.Status.HasFlag(UserStatus.Offline);
            if (setOffline)
            {
                score += 50; //todo: parameterize
                othersStrings.Add("Offline(50)");
            }
            var setInvisible = user.Status.HasFlag(UserStatus.Invisible);
            if (setInvisible)
            {
                score += 15; //todo: parameterize
                othersStrings.Add("Invisible(15)");
            }
            summaryStrings.Add($"Other: {string.Join(", ", othersStrings)}");

            var channel = await _client.GetChannelAsync(dbGuild.UserAnalysisChannel);
            if (channel is null) //todo: set channel back to 0
                return;

            //todo: send message?
        }
    }
}
