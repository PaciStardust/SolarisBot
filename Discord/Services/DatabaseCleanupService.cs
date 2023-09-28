using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;

namespace SolarisBot.Discord.Services
{
    /// <summary>
    /// Handles DatabaseCleanup for some discord events
    /// </summary>
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
            _client.RoleDeleted += OnRoleDeletedCheckForDbDuplicateAsync;
            _client.ChannelDestroyed += OnChannelDestroyedRemoveRemindersAsync;
            _client.UserLeft += OnUserLeftHandleAsync;
            _client.LeftGuild += OnLeftGuildRemoveGuildAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
        #endregion

        #region Events - OnUserLeft
        /// <summary>
        /// Handles all OnUserLeft events
        /// </summary>
        private async Task OnUserLeftHandleAsync(SocketGuild guild, SocketUser user)
        {
            await OnUserLeftRemoveQuotesAsync(guild, user);
            await OnUserLeftRemoveRemindersAsync(guild, user);
        }

        /// <summary>
        /// Deletes associated DbQuotes for left user
        /// </summary>
        private async Task OnUserLeftRemoveQuotesAsync(SocketGuild guild, SocketUser user)
        {
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var quotes = await dbCtx.Quotes.Where(x => x.GuildId == guild.Id && x.CreatorId == user.Id).ToArrayAsync();
            if (quotes.Length == 0)
                return;

            _logger.LogDebug("Removing {quotes} related quotes for left user {user} in guild {guild}", quotes.Length, user.Log(), guild.Log());
            dbCtx.Quotes.RemoveRange(quotes);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err != null)
                _logger.LogError(err, "Failed to remove {quotes} related quotes for left user {user} in guild {guild}", quotes.Length, user.Log(), guild.Log());
            else
                _logger.LogInformation("Removed {quotes} related quotes for left user {user} in guild {guild}", quotes.Length, user.Log(), guild.Log());
        }

        /// <summary>
        /// Deletes associated DbReminders for left user
        /// </summary>
        private async Task OnUserLeftRemoveRemindersAsync(SocketGuild guild, SocketUser user)
        {
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var reminders = await dbCtx.Reminders.Where(x => x.GuildId == guild.Id && x.UserId == user.Id).ToArrayAsync();
            if (reminders.Length == 0)
                return;

            _logger.LogDebug("Removing {reminders} related reminders for left user {user} in guild {guild}", reminders.Length, user.Log(), guild.Log());
            var err = await RemoveRemindersAsync(reminders, dbCtx);
            if (err != null)
                _logger.LogError(err, "Failed to remove {reminders} related reminders for left user {user} in guild {guild}", reminders.Length, user.Log(), guild.Log());
            else
                _logger.LogInformation("Removed {reminders} related reminders for left user {user} in guild {guild}", reminders.Length, user.Log(), guild.Log());
        }
        #endregion

        #region Events - Other
        /// <summary>
        /// Deleted DbRoles if associated with a deleted role
        /// </summary>
        private async Task OnRoleDeletedCheckForDbDuplicateAsync(SocketRole role)
        {
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var dbRole = await dbCtx.Roles.FirstOrDefaultAsync(x => x.RoleId == role.Id);
            if (dbRole == null)
                return;

            _logger.LogDebug("Deleting match {dbRole} for deleted role {role} in DB", dbRole, role.Log());
            dbCtx.Roles.Remove(dbRole);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err != null)
                _logger.LogError(err, "Failed to delete match {dbRole} for deleted role {role} in DB", dbRole, role.Log());
            else
                _logger.LogInformation("Deleted match {dbRole} for deleted role {role} in DB", dbRole, role.Log());
        }

        /// <summary>
        /// Removes all DbReminders associated with a destroyed channel
        /// </summary>
        private async Task OnChannelDestroyedRemoveRemindersAsync(SocketChannel channel)
        {
            if (channel is not IGuildChannel gChannel)
                return;

            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var reminders = await dbCtx.Reminders.Where(x => x.ChannelId == channel.Id).ToArrayAsync();
            if (reminders.Length == 0)
                return;

            _logger.LogDebug("Removing {reminders} related reminders for deleted channel {channel} in guild {guild}", reminders.Length, gChannel.Log(), gChannel.Guild.Log());
            var err = await RemoveRemindersAsync(reminders, dbCtx);
            if (err != null)
                _logger.LogError(err, "Failed to remove {reminders} related reminders for deleted channel {channel} in guild {guild}", reminders.Length, gChannel.Log(), gChannel.Guild.Log());
            else
                _logger.LogInformation("Removed {reminders} related reminders for deleted channel {channel} in guild {guild}", reminders.Length, gChannel.Log(), gChannel.Guild.Log());
        }

        /// <summary>
        /// Removes DbGuild when leaving a guild
        /// </summary>
        private async Task OnLeftGuildRemoveGuildAsync(SocketGuild guild)
        {
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var dbGuild = await dbCtx.GetGuildByIdAsync(guild.Id);
            if (dbGuild == null)
                return;

            _logger.LogDebug("Removing guild for deleted guild {guild}", guild.Log());
            dbCtx.Guilds.Remove(dbGuild);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err != null)
                _logger.LogError(err, "Failed to remove guild for deleted guild {guild}", guild.Log());
            else
                _logger.LogDebug("Removed guild for deleted guild {guild}", guild.Log());
        }
        #endregion

        #region Utils
        private static async Task<Exception?> RemoveRemindersAsync(DbReminder[] reminders, DatabaseContext ctx)
        {
            ctx.Reminders.RemoveRange(reminders);
            return (await ctx.TrySaveChangesAsync()).Item2;
        }
        #endregion
    }
}
