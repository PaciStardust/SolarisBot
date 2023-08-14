using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Discord
{
    internal sealed class DiscordClientService : IHostedService
    {
        private readonly BotConfig _config;
        private readonly ILogger<DiscordClientService> _logger;
        private readonly DiscordSocketClient _client;

        public DiscordClientService(BotConfig config, ILogger<DiscordClientService> logger, DiscordSocketClient client)
        {
            _client = client;
            _config = config;
            _logger = logger;

            _client.Log += logMessage => logMessage.Log(_logger);
        }

        public async Task StartAsync(CancellationToken cToken)
        {
            await _client.LoginAsync(TokenType.Bot, _config.Token);
            await _client.StartAsync();
        }

        public async Task StopAsync(CancellationToken cToken)
        {
            await _client.LogoutAsync();
            await _client.StopAsync();
        }
    }
}
