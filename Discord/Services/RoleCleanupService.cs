using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Services
{
    /// <summary>
    /// Service for handling removal and applying of roles
    /// </summary>
    [AutoLoad]
    internal sealed class RoleCleanupService : IHostedService
    {
        private readonly ILogger<RoleCleanupService> _logger;
        private readonly DiscordSocketClient _client;

        public RoleCleanupService(ILogger<RoleCleanupService> logger, DiscordSocketClient client)
        {
            _client = client;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.GuildMemberUpdated += CheckForLeftoverCustomColorRoleOnRemovalAsync;
            _client.UserLeft += CheckForLeftoverCustomColorRoleOnLeftAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task CheckForLeftoverCustomColorRoleOnRemovalAsync(Cacheable<SocketGuildUser, ulong> oldData, SocketGuildUser newUser)
        {
            var oldUser = oldData.Value;
            if (oldUser is null || oldUser.Roles.Count <= newUser.Roles.Count)
                return;

            var removedRole = oldUser.Roles.FirstOrDefault(x => !newUser.Roles.Contains(x));
            if (removedRole is null || removedRole.Name != DiscordUtils.GetCustomColorRoleName(newUser))
                return;

            await TryDeleteLeftoverCustomColorRoleAsync(removedRole, newUser, newUser.Guild);
        }

        private async Task CheckForLeftoverCustomColorRoleOnLeftAsync(SocketGuild guild, SocketUser user)
        {
            var customColorRole = guild.Roles.FirstOrDefault(x => x.Name == DiscordUtils.GetCustomColorRoleName(user));
            if (customColorRole is null)
                return;

            await TryDeleteLeftoverCustomColorRoleAsync(customColorRole, user, guild);
        }

        /// <summary>
        /// Deletes a custom color role
        /// </summary>
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
    }
}
