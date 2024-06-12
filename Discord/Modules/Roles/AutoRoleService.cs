using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles
{
    [Module("roles/autorole"), AutoLoadService]
    internal sealed class AutoRoleService : IHostedService
    {
        private readonly ILogger<AutoRoleService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DbService _dbService;

        public AutoRoleService(ILogger<AutoRoleService> logger, DiscordSocketClient client, DbService dbService)
        {
            _client = client;
            _dbService = dbService;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.UserJoined += ApplyAutoRoleAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.UserJoined -= ApplyAutoRoleAsync;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Automatically applies a role to a user on join when set up
        /// </summary>
        private async Task ApplyAutoRoleAsync(SocketGuildUser user)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetGuildByIdAsync(user.Guild.Id);
            if (dbGuild is null || dbGuild.AutoRoleId == ulong.MinValue || user.Guild.FindRole(dbGuild.AutoRoleId) is null) //todo: [FEATURE] Notify for this?
                return;

            try
            {
                _logger.LogDebug("Applying auto-role {auto-role} to user {user} in guild {guild}", dbGuild.AutoRoleId, user.Log(), user.Guild.Log());
                await user.AddRoleAsync(dbGuild.AutoRoleId);
                _logger.LogInformation("Applied auto-role {auto-role} to user {user} in guild {guild}", dbGuild.AutoRoleId, user.Log(), user.Guild.Log());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply auto-role {auto-role} to user {user} in guild {guild}", dbGuild.AutoRoleId, user.Log(), user.Guild.Log());
            }
        }
    }
}
