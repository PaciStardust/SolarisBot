using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
namespace SolarisBot.Discord.Services
{
    /// <summary>
    /// Service for handling removal of roles
    /// </summary>
    internal sealed class RoleDisposalService : IHostedService
    {
        private readonly ILogger<RoleDisposalService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseContext _dbContext;

        public RoleDisposalService(ILogger<RoleDisposalService> logger, DiscordSocketClient client, DatabaseContext dbContext)
        {
            _client = client;
            _dbContext = dbContext;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.RoleDeleted += CheckForDbRoleDuplicateAsync;
            _client.GuildMemberUpdated += CheckForLeftoverCustomColorRoleOnRemovalAsync;
            _client.UserLeft += CheckForLeftoverCustomColorRoleOnLeftAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        #region OnRoleDeleted
        private async Task CheckForDbRoleDuplicateAsync(SocketRole role)
        {
            _logger.LogDebug("Role {role} has been deleted from discord, checking for match in DB", role.Log());
            var dbRole = _dbContext.Roles.FirstOrDefault(x => x.RId == role.Id);
            if (dbRole == null)
            {
                _logger.LogDebug("No matching role to delete found for discord role {role}", role.Log());
                return;
            }

            _logger.LogDebug("Deleting match {dbRole} for deleted role {role} in DB", dbRole, role.Log());
            _dbContext.Roles.Remove(dbRole);

            if (await _dbContext.SaveChangesAsync() == -1)
                _logger.LogError("Failed to delete match {dbRole} for deleted role {role} in DB", dbRole, role.Log());
            else
                _logger.LogInformation("Deleted match {dbRole} for deleted role {role} in DB", dbRole, role.Log());
        }
        #endregion

        #region Deleting Leftover Custom Color Role
        private async Task CheckForLeftoverCustomColorRoleOnRemovalAsync(Cacheable<SocketGuildUser, ulong> oldData, SocketGuildUser newUser)
        {
            var oldUser = oldData.Value;
            if (oldUser.Roles.Count <= newUser.Roles.Count)
                return;

            var removedRole = oldUser.Roles.FirstOrDefault(x => !newUser.Roles.Contains(x));
            if (removedRole == null || removedRole.Name != DiscordUtils.GetCustomColorRoleName(newUser))
                return;

            await TryDeleteLeftoverCustomColorRoleAsync(removedRole, newUser, newUser.Guild);
        }

        private async Task CheckForLeftoverCustomColorRoleOnLeftAsync(SocketGuild guild, SocketUser user)
        {
            var customColorRole = guild.Roles.FirstOrDefault(x => x.Name == DiscordUtils.GetCustomColorRoleName(user));
            if (customColorRole == null)
                return;

            await TryDeleteLeftoverCustomColorRoleAsync(customColorRole, user, guild);
        }

        private async Task TryDeleteLeftoverCustomColorRoleAsync(SocketRole role, SocketUser user, SocketGuild guild)
        {
            try
            {
                _logger.LogDebug("Deleting leftover custom color role {role} in guild {guild} from owner {user}", role.Log(), guild.Log(), user.Log());
                await role.DeleteAsync();
                _logger.LogInformation("Deleted leftover custom color role {role} in guild {guild} from owner {user}", role.Log(), guild.Log(), user.Log());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deleted leftover custom color role {role} in guild {guild} from owner {user}", role.Log(), guild.Log(), user.Log());
            }
        }
        #endregion
    }
}
