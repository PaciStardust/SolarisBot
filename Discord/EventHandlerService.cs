using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;

namespace SolarisBot.Discord
{
    internal sealed class EventHandlerService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly ILogger<EventHandlerService> _logger;
        private readonly IServiceProvider _services;

        public EventHandlerService(DiscordSocketClient client, ILogger<EventHandlerService> logger, IServiceProvider services)
        {
            _client = client;
            _logger = logger;
            _services = services;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.RoleDeleted += OnRoleDeletedAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Deletes roles from Roles Table if deleted in discord
        /// </summary>
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
    }
}
