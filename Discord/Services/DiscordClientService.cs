using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Discord.Common;

namespace SolarisBot.Discord.Services
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
        }

        public async Task StartAsync(CancellationToken cToken)
        {
            _client.Log += OnLog;
            _client.Ready += OnReadyAsync;

            await _client.LoginAsync(TokenType.Bot, _config.Token);
            await _client.StartAsync();
        }

        public async Task StopAsync(CancellationToken cToken)
        {
            _client.Log -= OnLog;
            _client.Ready -= OnReadyAsync;

            await _client.LogoutAsync();
            await _client.StopAsync();
        }

        private Task OnLog(LogMessage logMessage)
            => logMessage.Log(_logger);

        private async Task OnReadyAsync()
        {
            try
            {
                await _client.SetGameAsync(_config.DefaultStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ready up client extras");
                //throw;
            }
        }
    }
}
