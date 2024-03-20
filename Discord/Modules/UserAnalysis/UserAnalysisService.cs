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
        private readonly BotConfig _config;
        private readonly IServiceProvider _services;

        public UserAnalysisService(ILogger<UserAnalysisService> logger, DiscordSocketClient client, DatabaseContext dbCtx, BotConfig config, IServiceProvider services)
        {
            _logger = logger;
            _client = client;
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

        private async Task EvaluateUserCredibilityAsync(SocketGuildUser user)
        {
            if (user.IsWebhook || user.IsBot)
                return;

            var dbCtx = _services.GetRequiredService<DatabaseContext>();
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
                _logger.LogInformation("Sending user analysis {analyis} to channel {channel}", analysis.Log(analysisScore), channel.Log());
                await msgChannel.SendMessageAsync(actionText, embed: analysis.GenerateSummaryEmbed(analysisScore), components: componentBuilder.Build());
                _logger.LogInformation("Sent user analysis {analyis} to channel {channel}", analysis.Log(analysisScore), channel.Log());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending user analysis {analyis} to channel {channel}", analysis.Log(analysisScore), channel.Log());
            }
        }

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
                        _logger.LogDebug("Kicking user {user} from guild {guild}, user analysis score {score} satisfies kick score {kickScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisKickAt);
                        await targetUser.KickAsync($"Automatically kicked via user analysis ({analysisScore} >= {dbGuild.UserAnalysisKickAt} Score)");
                        _logger.LogInformation("Kicked user {user} from guild {guild}, user analysis score {score} satisfies kick score {kickScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisKickAt);
                        return ($"was automatically kicked *({analysisScore} >= {dbGuild.UserAnalysisKickAt} Score)*", ModerationAction.Kick);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed kicking user {user} from guild {guild}, user analysis score {score} satisfies kick score {kickScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisKickAt);
                        return ($"was not kicked *({ex.Message})*", ModerationAction.Warn);
                    }

                case ModerationAction.Ban:
                    if (!targetUser.Guild.CurrentUser.GuildPermissions.BanMembers)
                        return ("was not banned as permission is missing", ModerationAction.Warn);
                    try
                    {
                        _logger.LogDebug("Banning user {user} from guild {guild}, user analysis score {score} satisfies ban score {banScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisBanAt);
                        await targetUser.KickAsync($"Automatically banned via user analysis ({analysisScore} >= {dbGuild.UserAnalysisBanAt} Score)");
                        _logger.LogInformation("Banned user {user} from guild {guild}, user analysis score {score} satisfies ban score {banScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisBanAt);
                        return ($"was automatically banned *({analysisScore} >= {dbGuild.UserAnalysisBanAt} Score)*", ModerationAction.Ban);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed banned user {user} from guild {guild}, user analysis score {score} satisfies ban score {banScore}", targetUser.Log(), targetUser.Guild.Log(), analysisScore, dbGuild.UserAnalysisBanAt);
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
    }
}
