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
        private readonly IServiceProvider _services;

        public BridgeService(ILogger<BridgeService> logger, DiscordSocketClient client, IServiceProvider services)
        {
            _logger = logger;
            _client = client;
            _services = services;
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

            var dbCtx = _services.GetRequiredService<DatabaseContext>();
            var bridges = await dbCtx.Bridges.ForChannel(message.Channel.Id).ToArrayAsync();
            if (bridges.Length == 0)
                return;

            foreach (var bridge in bridges)
            {
                var channelId = bridge.ChannelAId == message.Channel.Id ? bridge.ChannelBId : bridge.ChannelAId;
                var channel = await _client.GetChannelAsync(channelId);

                if (channel is null)
                {
                    var tempCtx = _services.GetRequiredService<DatabaseContext>();
                    tempCtx.Bridges.Remove(bridge);
                    _logger.LogDebug("Deleting bridge {bridge}, could not locate channel {channel}", bridge, channelId);
                    var (_, err) = await tempCtx.TrySaveChangesAsync();
                    if (err is not null)
                        _logger.LogError(err, "Failed deleting bridge {bridge}, could not locate channel {channel}", bridge, channelId);
                    else
                        _logger.LogInformation("Deleted bridge {bridge}, could not locate channel {channel}", bridge, channelId);

                    var originChannel = await _client.GetChannelAsync(bridge.ChannelAId == channelId ? bridge.ChannelBId : bridge.ChannelAId);
                    if (originChannel is null || originChannel is not IMessageChannel msgOriginChannel)
                        continue;

                    await BridgeHelper.TryNotifyChannelForBridgeDeletionAsync(msgOriginChannel, null, bridge, _logger, channelId == bridge.ChannelBId);
                    continue;
                }

                try
                {
                    _logger.LogDebug("Sending message from user {user} via bridge {bridge}", message.Author.Log(), bridge);
                    await ((IMessageChannel)channel).SendMessageAsync($"**[{bridge.Name}]{message.Author.GlobalName}:** {message.CleanContent}");
                    _logger.LogInformation("Sent message from user {user} via bridge {bridge}", message.Author.Log(), bridge);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed sending message via brigde {bridge}", bridge);
                }
            }
        }
    }
}
