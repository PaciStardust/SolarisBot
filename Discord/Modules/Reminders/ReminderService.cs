using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Timers;

namespace SolarisBot.Discord.Modules.Reminders
{
    [Module("reminders"), AutoLoadService]
    internal sealed class ReminderService : IHostedService
    {
        private readonly ILogger<ReminderService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseService _dbService;
        private readonly System.Timers.Timer _timer;

        public ReminderService(ILogger<ReminderService> logger, DiscordSocketClient client, DatabaseService dbService)
        {
            _client = client;
            _dbService = dbService;
            _logger = logger;
            _timer = new System.Timers.Timer(TimeSpan.FromSeconds(30));
            _timer.Elapsed += new ElapsedEventHandler(RemindUsersAsync);
        }

        #region Start / Stop
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _timer.Start();
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer.Stop();
            return Task.CompletedTask;
        }
        #endregion

        #region Reminding
        private async void RemindUsersAsync(object? source, ElapsedEventArgs args)
            => await RemindUsersAsync();

        private async Task RemindUsersAsync()
        {
            if (_client.LoginState != LoginState.LoggedIn)
                return;

            var nowUnix = Utils.GetCurrentUnix();
            //_logger.LogDebug("Checking Database for reminders");
            using var dbCtx = _dbService.GetContext();
            var reminders = await dbCtx.Reminders.FromSql($"SELECT * FROM Reminders WHERE RemindAt <= {nowUnix}").ToArrayAsync(); //UInt equality not supported
            if (reminders.Length == 0)
            {
                //_logger.LogDebug("Checked database for reminders, none found");
                return;
            }

            _logger.LogDebug("Sending out {reminders} reminders", reminders.Length);
            var remindersToDelete = new List<DbReminder>();
            foreach (var reminder in reminders)
            {
                var result = await SendReminderAsync(reminder);
                if (result)
                {
                    remindersToDelete.Add(reminder);
                }
            }

            if (remindersToDelete.Count == 0)
            {
                _logger.LogInformation("Reminders finished, no reminders to delete");
                return;
            }

            _logger.LogInformation("Reminders finished, removing {reminders} reminders from DB", remindersToDelete.Count);
            dbCtx.Reminders.RemoveRange(remindersToDelete);

            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
                _logger.LogError(err, "Failed to remove {reminders} reminders from DB", remindersToDelete.Count);
            else
                _logger.LogInformation("Removed {reminders} reminders from DB", remindersToDelete.Count);
        }

        private async Task<bool> SendReminderAsync(DbReminder reminder)
        {
            try
            {
                var channel = await _client.GetChannelAsync(reminder.ChannelId);
                if (channel is null || channel is not IMessageChannel msgChannel)
                {
                    _logger.LogDebug("Failed to find channel {channel} for reminder {reminder}", reminder.ChannelId, reminder);
                    return true;
                }

                var user = await channel.GetUserAsync(reminder.UserId);
                if (user is null)
                {
                    _logger.LogDebug("Failed to find user {user} for reminder {reminder}", reminder.UserId, reminder);
                    return true;
                }
                _logger.LogDebug("Received data for channel {channel} and user {user} for reminder {reminder}", msgChannel.Log(), user.Log(), reminder);

                _logger.LogDebug("Reminding user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GuildId);
                var embed = EmbedFactory.Default($"**{reminder.Text}**\n*(Created <t:{reminder.CreatedAt}:f>)*");
                await msgChannel.SendMessageAsync($"Here is your reminder <@{reminder.UserId}>!", embed: embed);
                _logger.LogInformation("Reminded user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GuildId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed reminding user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GuildId);
                return false;
            }

            return true;
        }
        #endregion
    }
}
