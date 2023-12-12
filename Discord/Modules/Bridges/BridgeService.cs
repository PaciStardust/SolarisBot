using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Database.Models;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Bridges
{
    [Module("bridges"), AutoLoadService]
    internal class BridgeService : IHostedService //todo: [TESTING] Do everyone/here pings get filtered out?
    {
        private readonly DatabaseContext _dbCtx;
        private readonly ILogger<BridgeService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;

        public BridgeService(ILogger<BridgeService> logger, DiscordSocketClient client, DatabaseContext dbCtx, IServiceProvider services)
        {
            _logger = logger;
            _client = client;
            _dbCtx = dbCtx;
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

            var bridges = await _dbCtx.Bridges.ForChannel(message.Channel.Id).ToArrayAsync();
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
                    continue;
                }

                try
                {
                    _logger.LogDebug("Sending message via bridge {bridge}", bridge);
                    await ((IMessageChannel)channel).SendMessageAsync($"**{message.Author.GlobalName} via {bridge.Name}:** {message.CleanContent}");
                    _logger.LogInformation("Sent message via bridge {bridge}", bridge);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed sending message via brigde {bridge}", bridge);
                }
            }
        }
    }
}
