using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Bridges
{
    [Module("bridges"), AutoLoadService]
    internal class BridgeService : IHostedService
    {
        private readonly ILogger<BridgeService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseService _dbService;

        public BridgeService(ILogger<BridgeService> logger, DiscordSocketClient client, DatabaseService dbService)
        {
            _logger = logger;
            _client = client;
            _dbService = dbService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived += CheckForBridgesAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived -= CheckForBridgesAsync;
            return Task.CompletedTask;
        }

        private async Task CheckForBridgesAsync(SocketMessage message)
        {
            if (message.Author.IsWebhook || message.Author.IsBot)
                return;

            using var dbCtx = _dbService.GetContext();
            var bridges = await dbCtx.Bridges.ForChannel(message.Channel.Id).ToArrayAsync();
            if (bridges.Length == 0)
                return;

            foreach (var bridge in bridges)
            {
                var targetChannelId = bridge.ChannelAId == message.Channel.Id ? bridge.ChannelBId : bridge.ChannelAId;
                var targetChannel = await _client.GetChannelAsync(targetChannelId);

                if (targetChannel is null || targetChannel is not IMessageChannel targetMessageChannel)
                {
                    await DeleteBridgeAsync(bridge, targetChannelId);
                }
                else
                {
                    await SendMessageViaBridgeAsync(message, bridge, targetMessageChannel);
                }
            }
        }

        private async Task SendMessageViaBridgeAsync(SocketMessage message, DbBridge bridge, IMessageChannel targetMessageChannel)
        {
            try
            {
                _logger.LogDebug("Sending message from user {user} via bridge {bridge}", message.Author.Log(), bridge);
                await targetMessageChannel.SendMessageAsync($"**[{bridge.Name}] {message.Author.GlobalName}:** {message.CleanContent}");
                _logger.LogInformation("Sent message from user {user} via bridge {bridge}", message.Author.Log(), bridge);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed sending message via brigde {bridge}", bridge); //todo: [FEATURE] notify for this?
            }
        }

        private async Task DeleteBridgeAsync(DbBridge bridge, ulong missingChannelId)
        {
            using var tempCtx = _dbService.GetContext();
            tempCtx.Bridges.Remove(bridge);

            _logger.LogDebug("Deleting bridge {bridge}, could not locate channel {channel}", bridge, missingChannelId);
            var (_, err) = await tempCtx.TrySaveChangesAsync();
            if (err is not null)
                _logger.LogError(err, "Failed deleting bridge {bridge}, could not locate channel {channel}", bridge, missingChannelId);
            else
                _logger.LogInformation("Deleted bridge {bridge}, could not locate channel {channel}", bridge, missingChannelId);

            var originChannelId = bridge.ChannelAId == missingChannelId ? bridge.ChannelBId : bridge.ChannelAId;
            var originChannel = await _client.GetChannelAsync(originChannelId);
            if (originChannel is null || originChannel is not IMessageChannel msgOriginChannel)
            {
                _logger.LogInformation("Could not notify origin channel {channel} of broken bridge as it could not be located", originChannelId);
                return;
            }

            await BridgeHelper.TryNotifyChannelForBridgeDeletionAsync(msgOriginChannel, null, bridge, _logger, missingChannelId == bridge.ChannelBId);
        }
    }
}
