using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.UserAnalysis
{
    [Module("useranalysis"), AutoLoadService]
    internal class UserAnalysisService : IHostedService
    {
        private readonly ILogger<UserAnalysisService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseContext _dbCtx;
        private readonly BotConfig _config;
        private readonly IServiceProvider _services;

        public UserAnalysisService(ILogger<UserAnalysisService> logger, DiscordSocketClient client, DatabaseContext dbCtx, BotConfig config, IServiceProvider services)
        {
            _logger = logger;
            _client = client;
            _dbCtx = dbCtx;
            _config = config;
            _services = services;
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
        //todo: quarantine?

        private async Task EvaluateUserCredibilityAsync(SocketGuildUser user)
        {
            if (user.IsWebhook || user.IsBot)
                return;

            var dbGuild = await _dbCtx.GetGuildByIdAsync(user.Guild.Id);
            if (dbGuild is null || dbGuild.UserAnalysisChannel == ulong.MinValue)
                return;

            var analysis = UserAnalysis.ForUser(user, _config);

            var channel = await _client.GetChannelAsync(dbGuild.UserAnalysisChannel);
            if (channel is null || channel is not IMessageChannel msgChannel)
            {
                _logger.LogDebug("Resetting UserAnalysisChannel for guild {guild}, could not locate channel withid {channelId}", dbGuild, dbGuild.UserAnalysisChannel);
                dbGuild.UserAnalysisChannel = 0;
                var dbCtx = _services.GetRequiredService<DatabaseContext>();
                dbCtx.GuildConfigs.Update(dbGuild);
                var (_, err) = await dbCtx.TrySaveChangesAsync();
                if (err is not null)
                    _logger.LogError(err, "Failed resetting UserAnalysisChannel for guild {guild}, could not locate channel withid {channelId}", dbGuild, dbGuild.UserAnalysisChannel);
                else
                    _logger.LogInformation("Reset UserAnalysisChannel for guild {guild}, could not locate channel withid {channelId}", dbGuild, dbGuild.UserAnalysisChannel);
                return;
            }

            var analysisScore = analysis.CalculateScore();
            try
            {
                _logger.LogInformation("Sending user analysis {analyis} to channel {channel}", analysis.Log(analysisScore), channel.Log());
                await msgChannel.SendMessageAsync(embed: analysis.GenerateSummaryEmbed(analysisScore));
                _logger.LogInformation("Sent user analysis {analyis} to channel {channel}", analysis.Log(analysisScore), channel.Log());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending user analysis {analyis} to channel {channel}", analysis.Log(analysisScore), channel.Log());
            }
        }
    }
}
