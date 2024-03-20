using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Modules.Bridges;

namespace SolarisBot.Discord.Services
{
    /// <summary>
    /// Handles DatabaseCleanup for some discord events
    /// </summary>
    [AutoLoadService]
    internal sealed class DatabaseCleanupService : IHostedService
    {
        private readonly ILogger<DatabaseCleanupService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _provider;

        public DatabaseCleanupService(ILogger<DatabaseCleanupService> logger, DiscordSocketClient client, IServiceProvider provider)
        {
            _client = client;
            _provider = provider;
            _logger = logger;
        }

        #region Start / Stop
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.RoleDeleted += OnRoleDeletedHandleAsync;
            _client.ChannelDestroyed += OnChannelDestroyedHandleAsync;
            _client.UserLeft += OnUserLeftHandleAsync;
            _client.LeftGuild += OnLeftGuildHandleAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.RoleDeleted -= OnRoleDeletedHandleAsync;
            _client.ChannelDestroyed -= OnChannelDestroyedHandleAsync;
            _client.UserLeft -= OnUserLeftHandleAsync;
            _client.LeftGuild -= OnLeftGuildHandleAsync;
            return Task.CompletedTask;
        }
        #endregion

        #region Events - OnUserLeft
        /// <summary>
        /// Handles all OnUserLeft events
        /// </summary>
        private async Task OnUserLeftHandleAsync(SocketGuild guild, SocketUser user)
        {
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();

            var changes = await OnUserLeftRemoveQuotesAsync(dbCtx, guild, user);
            changes = changes || await OnUserLeftRemoveRemindersAsync(dbCtx, guild, user);

            if (!changes)
                return;

            _logger.LogDebug("Deleting references to user {user} in guild {guild} from DB", user.Log(), guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
                _logger.LogError(err, "Failed to delete references to user {user} in guild {guild} from DB", user.Log(), guild.Log());
            else
                _logger.LogInformation("Deleted references to user {user} in guild {guild} from DB", user.Log(), guild.Log());
        }

        /// <summary>
        /// Deletes associated DbQuotes for left user
        /// </summary>
        private async Task<bool> OnUserLeftRemoveQuotesAsync(DatabaseContext dbCtx, SocketGuild guild, SocketUser user)
        {
            var quotes = await dbCtx.Quotes.ForGuild(guild.Id).Where(x => x.CreatorId == user.Id).ToArrayAsync();
            if (quotes.Length == 0)
                return false;

            _logger.LogDebug("Removing {quotes} related quotes for left user {user} in guild {guild}", quotes.Length, user.Log(), guild.Log());
            dbCtx.Quotes.RemoveRange(quotes);
            return true;
        }

        /// <summary>
        /// Deletes associated DbReminders for left user
        /// </summary>
        private async Task<bool> OnUserLeftRemoveRemindersAsync(DatabaseContext dbCtx, SocketGuild guild, SocketUser user)
        {
            var reminders = await dbCtx.Reminders.ForGuild(guild.Id).ForUser(user.Id).ToArrayAsync();
            if (reminders.Length == 0)
                return false;

            _logger.LogDebug("Removing {reminders} related reminders for left user {user} in guild {guild}", reminders.Length, user.Log(), guild.Log());
            dbCtx.Reminders.RemoveRange(reminders);
            return true;
        }
        #endregion

        #region Events - OnRoleDeleted
        /// <summary>
        /// Cleans up any role references in DB for deleted role
        /// </summary>
        private async Task OnRoleDeletedHandleAsync(SocketRole role)
        {
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            
            var changes = await OnRoleDeletedCleanRoleSettingsAsync(dbCtx, role);

            if (!changes)
                return;

            _logger.LogDebug("Deleting references to role {role} in DB", role.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
                _logger.LogError(err, "Failed to delete references to role {role} in DB", role.Log());
            else
                _logger.LogInformation("Deleted references to role {role} in DB", role.Log());
        }

        /// <summary>
        /// Removes RoleSetting if maz
        /// </summary>
        private async Task<bool> OnRoleDeletedCleanRoleSettingsAsync(DatabaseContext dbCtx, SocketRole role)
        {
            var dbRole = await dbCtx.RoleConfigs.FirstOrDefaultAsync(x => x.RoleId == role.Id);
            if (dbRole is null)
                return false;

            _logger.LogDebug("Deleting match {dbRole} for deleted role {role} in DB", dbRole, role.Log());
            dbCtx.RoleConfigs.Remove(dbRole);
            return true;
        }
        #endregion

        #region Events - OnChannelDestroyed
        /// <summary>
        /// Handles all OnChannelDestroyed events
        /// </summary>
        private async Task OnChannelDestroyedHandleAsync(SocketChannel channel)
        {
            if (channel is not IGuildChannel gChannel)
                return;

            var dbCtx = _provider.GetRequiredService<DatabaseContext>();

            var changes = await OnChannelDestroyedRemoveBridgesAsync(gChannel, dbCtx);
            changes = changes || await OnChannelDestroyedRemoveRemindersAsync(gChannel, dbCtx);

            if (!changes)
                return;

            _logger.LogDebug("Deleting references to channel {channel} in guild {guild} from DB", gChannel.Log(), gChannel.Guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
                _logger.LogError(err, "Failed to delete references to channel {channel} in guild {guild} from DB", gChannel.Log(), gChannel.Guild.Log());
            else
                _logger.LogInformation("Deleted references to channel {channel} in guild {guild} from DB", gChannel.Log(), gChannel.Guild.Log());
        }

        /// <summary>
        /// Removes all DbReminders associated with a destroyed channel
        /// </summary>
        private async Task<bool> OnChannelDestroyedRemoveRemindersAsync(IGuildChannel gChannel, DatabaseContext dbCtx)
        {
            var reminders = await dbCtx.Reminders.ForChannel(gChannel.Id).ToArrayAsync();
            if (reminders.Length == 0)
                return false;

            _logger.LogDebug("Removing {reminders} related reminders for deleted channel {channel} in guild {guild}", reminders.Length, gChannel.Log(), gChannel.Guild.Log());
            dbCtx.Reminders.RemoveRange(reminders);
            return true;
        }

        /// <summary>
        /// Removes all DbBridges connected with a destroyed channel
        /// </summary>
        private async Task<bool> OnChannelDestroyedRemoveBridgesAsync(IGuildChannel gChannel, DatabaseContext dbCtx)
        {
            var bridges = await dbCtx.Bridges.ForChannel(gChannel.Id).ToArrayAsync();
            if (bridges.Length == 0)
                return false;

            _logger.LogDebug("Removing {bridges} bridges for deleted channel {channel} in guild {guild}", bridges, gChannel.Log(), gChannel.Guild.Log());
            dbCtx.Bridges.RemoveRange(bridges);

            foreach (var bridge in bridges)
            {
                var useB = gChannel.Id == bridge.ChannelAId;

                var notifyChannel = await _client.GetChannelAsync(useB ? bridge.ChannelBId : bridge.ChannelAId);
                if (notifyChannel is null || notifyChannel is not IMessageChannel msgNotifyChannel)
                    continue;

                await BridgeHelper.TryNotifyChannelForBridgeDeletionAsync(msgNotifyChannel, gChannel, bridge, _logger, !useB);
            }
            return true;
        }
        #endregion

        #region Events - OnLeftGuild
        /// <summary>
        /// Handles all OnLeftGuild events
        /// </summary>
        private async Task OnLeftGuildHandleAsync(SocketGuild guild)
        {
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();

            var changes = await OnLeftGuildRemoveBridgesAsync(guild, dbCtx);
            changes = changes || await OnLeftGuildRemoveGuildAsync(guild, dbCtx);

            if (!changes)
                return;

            _logger.LogDebug("Deleting references to guild {guild} from DB", guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
                _logger.LogError(err, "Failed to delete references to guild {guild} from DB", guild.Log());
            else
                _logger.LogInformation("Deleted references to guild {guild} from DB", guild.Log());
        }

        /// <summary>
        /// Removes DbGuild when leaving a guild
        /// </summary>
        private async Task<bool> OnLeftGuildRemoveGuildAsync(SocketGuild guild, DatabaseContext dbCtx)
        {
            var dbGuild = await dbCtx.GetGuildByIdAsync(guild.Id);
            if (dbGuild is null)
                return false;

            _logger.LogDebug("Removing guild for deleted guild {guild}", guild.Log());
            dbCtx.GuildConfigs.Remove(dbGuild);
            return true;
        }

        /// <summary>
        /// Removes bridges when leaving a guild
        /// </summary>
        private async Task<bool> OnLeftGuildRemoveBridgesAsync(SocketGuild guild, DatabaseContext dbCtx)
        {
            var bridges = await dbCtx.Bridges.ForGuild(guild.Id).ToArrayAsync();
            if (bridges.Length == 0)
                return false;

            _logger.LogDebug("Removing {bridges} bridges for deleted guild {guild}", bridges.Length, guild.Log());
            dbCtx.Bridges.RemoveRange(bridges);

            foreach(var bridge in bridges)
            {
                if (bridge.GuildAId == guild.Id && bridge.GuildBId == guild.Id)
                    continue;

                var useB = guild.Id == bridge.GuildAId;

                var notifyChannel = await _client.GetChannelAsync(useB ? bridge.ChannelBId : bridge.ChannelAId);
                if (notifyChannel is null || notifyChannel is not IMessageChannel msgNotifyChannel)
                    continue;

                await BridgeHelper.TryNotifyChannelForBridgeDeletionAsync(msgNotifyChannel, null, bridge, _logger, !useB);
            }
            return true;
        }
        #endregion
    }
}
