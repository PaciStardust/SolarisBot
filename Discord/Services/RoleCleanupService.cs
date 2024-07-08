using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Database.Models;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Services
{
    /// <summary>
    /// Service for handling removal and applying of roles
    /// </summary>
    [AutoLoadService]
    internal sealed class RoleCleanupService : IHostedService
    {
        private readonly ILogger<RoleCleanupService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseService _dbService;

        public RoleCleanupService(ILogger<RoleCleanupService> logger, DiscordSocketClient client, DatabaseService dbService)
        {
            _client = client;
            _logger = logger;
            _dbService = dbService;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.GuildMemberUpdated += CheckForLeftoverCustomColorRoleOnRemovalAsync;
            _client.UserLeft += CheckForLeftoverCustomColorRoleOnLeftAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.GuildMemberUpdated -= CheckForLeftoverCustomColorRoleOnRemovalAsync;
            _client.UserLeft -= CheckForLeftoverCustomColorRoleOnLeftAsync;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Deletes leftover custom color role when removed from user
        /// </summary>
        /// <param name="oldData">User before removal</param>
        /// <param name="newUser">User after removal</param>
        private async Task CheckForLeftoverCustomColorRoleOnRemovalAsync(Cacheable<SocketGuildUser, ulong> oldData, SocketGuildUser newUser)
        {
            var oldUser = oldData.Value;
            if (oldUser is null || oldUser.Roles.Count <= newUser.Roles.Count)
                return;

            var removedRole = oldUser.Roles.FirstOrDefault(x => !newUser.Roles.Contains(x));
            if (removedRole is null)
                return;

            using var dbCtx = _dbService.GetContext();
            var matchFound = await dbCtx.CustomColorRoles.Where(x => x.RoleId == removedRole.Id).ForGuild(newUser.Guild.Id).ForUser(newUser.Id).AnyAsync();

            if (matchFound)
                await TryDeleteLeftoverCustomColorRoleAsync(removedRole, newUser, newUser.Guild);
        }

        /// <summary>
        /// Deletes leftover custom color role when user leaves guild
        /// </summary>
        /// <param name="guild">Guild user left</param>
        /// <param name="user">User leaving guild</param>
        /// <returns></returns>
        private async Task CheckForLeftoverCustomColorRoleOnLeftAsync(SocketGuild guild, SocketUser user)
        {
            using var dbCtx = _dbService.GetContext();
            var dbRole = await dbCtx.CustomColorRoles.ForGuild(guild.Id).ForUser(user.Id).FirstOrDefaultAsync();

            if (dbRole is null)
                return;

            var discordRole = guild.Roles.Where(x => x.Id == dbRole.RoleId).FirstOrDefault();
            if (discordRole is null)
                return;

            await TryDeleteLeftoverCustomColorRoleAsync(discordRole, user, guild);
        }

        /// <summary>
        /// Deletes a custom color role
        /// </summary>
        private async Task TryDeleteLeftoverCustomColorRoleAsync(SocketRole role, SocketUser user, SocketGuild guild)
        {
            try
            {
                _logger.LogTrace("Deleting leftover custom color role {role} in guild {guild} from owner {user}", role.Log(), guild.Log(), user.Log());
                await role.DeleteAsync();
                _logger.LogDebug("Deleted leftover custom color role {role} in guild {guild} from owner {user}", role.Log(), guild.Log(), user.Log());
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to deleted leftover custom color role {role} in guild {guild} from owner {user}", role.Log(), guild.Log(), user.Log());
            }
        }
    }
}
