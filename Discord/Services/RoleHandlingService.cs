using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;

namespace SolarisBot.Discord.Services
{
    /// <summary>
    /// Service for handling removal and applying of roles
    /// </summary>
    internal sealed class RoleHandlingService : IHostedService //todo: [FEATURE] Automatic loading via interface?
    {
        private readonly ILogger<RoleHandlingService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _provider;

        public RoleHandlingService(ILogger<RoleHandlingService> logger, DiscordSocketClient client, IServiceProvider provider)
        {
            _client = client;
            _provider = provider;
            _logger = logger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.GuildMemberUpdated += CheckForLeftoverCustomColorRoleOnRemovalAsync;
            _client.UserLeft += CheckForLeftoverCustomColorRoleOnLeftAsync;
            _client.UserJoined += ApplyAutoRoleAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Automatically applies a role to a user on join when set up
        /// </summary>
        private async Task ApplyAutoRoleAsync(SocketGuildUser user)
        {
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var dbGuild = await dbCtx.GetGuildByIdAsync(user.Guild.Id);
            if (dbGuild is null || dbGuild.AutoRoleId == 0)
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

        #region Deleting Leftover Custom Color Role
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
        #endregion
    }
}
