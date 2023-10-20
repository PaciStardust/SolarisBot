using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;

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
            _client.RoleDeleted += OnRoleDeletedHandleAsync;
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
            var quotes = await dbCtx.Quotes.Where(x => x.GuildId == guild.Id && x.CreatorId == user.Id).ToArrayAsync();
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
            var reminders = await dbCtx.Reminders.Where(x => x.GuildId == guild.Id && x.UserId == user.Id).ToArrayAsync();
            if (reminders.Length == 0)
                return false;

            _logger.LogDebug("Removing {reminders} related reminders for left user {user} in guild {guild}", reminders.Length, user.Log(), guild.Log());
            var err = await RemoveRemindersAsync(reminders, dbCtx);
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
            changes = changes || await OnRoleDeletedCleanGuildSettingsAsync(dbCtx, role);

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
            var dbRole = await dbCtx.RoleConfig.FirstOrDefaultAsync(x => x.RoleId == role.Id);
            if (dbRole is null)
                return false;

            _logger.LogDebug("Deleting match {dbRole} for deleted role {role} in DB", dbRole, role.Log());
            dbCtx.RoleConfig.Remove(dbRole);
            return true;
        }


        /// <summary>
        /// Updates GuildSettings if RoleId is contained in settings
        /// </summary>
        private async Task<bool> OnRoleDeletedCleanGuildSettingsAsync(DatabaseContext dbCtx, SocketRole role)
        {
            var guild = await dbCtx.GetGuildByIdAsync(role.Guild.Id);
            if (guild is null)
                return false;

            bool changeMade = false;

            if (guild.MagicRoleId == role.Id)
            {
                guild.MagicRoleId = 0;
                changeMade = true;
            }
            if (guild.VouchPermissionRoleId == role.Id)
            {
                guild.VouchPermissionRoleId = 0;
                changeMade = true;
            }
            if (guild.VouchRoleId == role.Id)
            {
                guild.VouchRoleId = 0;
                changeMade = true;
            }
            if (guild.CustomColorPermissionRoleId == role.Id)
            {
                guild.CustomColorPermissionRoleId = 0;
                changeMade = true;
            }
            if (guild.AutoRoleId == role.Id)
            {
                guild.AutoRoleId = 0;
                changeMade = true;
            }
            if (guild.SpellcheckRoleId == role.Id)
            {
                guild.SpellcheckRoleId = 0;
                changeMade = true;
            }

            if (!changeMade)
                return false;

            _logger.LogDebug("Removing references of role {role} from guild {guild} in DB", role.Log(), guild);
            dbCtx.Update(guild);
            return true;
        }
        #endregion

        #region Events - Other
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
            if (err is not null)
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
            if (dbGuild is null)
                return;

            _logger.LogDebug("Removing guild for deleted guild {guild}", guild.Log());
            dbCtx.GuildConfigs.Remove(dbGuild);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
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
