using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
namespace SolarisBot.Discord.Services
{
    /// <summary>
    /// Service for handling removal and applying of roles
    /// </summary>
    internal sealed class RoleHandlingService : IHostedService
    {
        private readonly ILogger<RoleHandlingService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseContext _dbContext;

        public RoleHandlingService(ILogger<RoleHandlingService> logger, DiscordSocketClient client, DatabaseContext dbContext)
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
            _client.UserJoined += ApplyAutoRoleAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task ApplyAutoRoleAsync(SocketGuildUser user) //todo: test
        {
            _logger.LogDebug("User {user} has joined guild {guild}, checking for auto-role", user.Log(), user.Guild.Log());
            var dbGuild = await _dbContext.GetGuildByIdAsync(user.Guild.Id);
            if (dbGuild == null || dbGuild.AutoRoleId == 0)
            {
                _logger.LogDebug("No auto-role specified for guild {guild}", user.Guild.Log());
                return;
            }

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

        private async Task CheckForDbRoleDuplicateAsync(SocketRole role)
        {
            _logger.LogDebug("Role {role} has been deleted from guild {guild}, checking for match in DB", role.Log(), role.Guild.Log());
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
