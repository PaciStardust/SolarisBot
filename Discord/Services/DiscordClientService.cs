using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;

namespace SolarisBot.Discord.Services
{
    internal sealed class DiscordClientService : IHostedService
    {
        private readonly BotConfig _config;
        private readonly ILogger<DiscordClientService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;

        public DiscordClientService(BotConfig config, ILogger<DiscordClientService> logger, DiscordSocketClient client, IServiceProvider services)
        {
            _client = client;
            _config = config;
            _logger = logger;
            _services = services;

            _client.Log += logMessage => logMessage.Log(_logger);

            RegisterSubsciptions();
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

        #region Subscriptions
        private void RegisterSubsciptions()
        {
            _client.Ready += OnReadyAsync;
            _client.RoleDeleted += OnRoleDeletedAsync;
        }

        private async Task OnReadyAsync() //todo: [TESTING] Does status get applied?
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

        private async Task OnRoleDeletedAsync(SocketRole role)
        {
            _logger.LogDebug("Role {roleName}({roleId}) has been deleted from discord, checking for match in DB", role.Name, role.Id);
            var dbContext = _services.GetRequiredService<DatabaseContext>();
            var dbRole = dbContext.Roles.FirstOrDefault(x => x.RId == role.Id);
            if (dbRole == null)
                return;

            dbContext.Roles.Remove(dbRole);
            var res = await dbContext.SaveChangesAsync();

            if (res == -1)
                _logger.LogError("Deleted role {roleName}({roleId}) has found match in DB as {identifier} and attempt to remove has failed", role.Name, role.Id, dbRole.Name);
            else
                _logger.LogInformation("Deleted role {roleName}({roleId}) has found match in DB as {identifier} and has been removed", role.Name, role.Id, dbRole.Name);
        }
        #endregion
    }
}
