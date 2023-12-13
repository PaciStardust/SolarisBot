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

            var analysis = UserAnalysis.ForUser(user, _config);

            var channel = await _client.GetChannelAsync(dbGuild.UserAnalysisChannel);
            if (channel is null) //todo: set channel back to 0
                return;

            //todo: send message?
        }
    }
}
