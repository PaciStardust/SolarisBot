using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using System.Timers;

namespace SolarisBot.Discord.Services
{
    internal class ReminderService : IHostedService
    {
        private readonly ILogger<ReminderService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseContext _dbContext;
        private readonly System.Timers.Timer _timer;

        public ReminderService(ILogger<ReminderService> logger, DiscordSocketClient client, DatabaseContext dbContext)
        {
            _client = client;
            _dbContext = dbContext;
            _logger = logger;
            _timer = new System.Timers.Timer(TimeSpan.FromSeconds(30));
            _timer.Elapsed += new ElapsedEventHandler(RemindUsersAsync);
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await RemindUsersAsync();
            _timer.Start();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer.Stop();
            return Task.CompletedTask;
        }

        private async void RemindUsersAsync(object? source, ElapsedEventArgs args)
            => await RemindUsersAsync();

        private async Task RemindUsersAsync()
        {
            if (_client.LoginState != LoginState.LoggedIn)
                return;

            var nowUnix = Utils.GetCurrentUnix(_logger);
            var reminders = _dbContext.Reminders.FromSqlRaw($"SELECT * FROM reminders WHERE time <= {nowUnix}"); //UInt equality not supported
            var remCount = reminders.Count();
            if (remCount == 0)
                return;

            _logger.LogDebug("Sending out {reminders} reminders", remCount);
            foreach (var reminder in reminders)
            {
                try
                {
                    var channel = await _client.GetChannelAsync(reminder.ChannelId);
                    if (channel != null && channel is IMessageChannel msgChannel)
                    {
                        _logger.LogDebug("Reminding user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GId);
                        var embed = DiscordUtils.Embed($"Reminder", $"**{reminder.Text}**\n*(Created <t:{reminder.Created}:f>)*");
                        await msgChannel.SendMessageAsync($"Here is your reminder <@{reminder.UserId}>!", embed: embed);
                        _logger.LogInformation("Reminded user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed reminding user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GId);
                }
            }

            _logger.LogInformation("Reminders finished, removing {reminders} reminders from DB", remCount);
            _dbContext.Reminders.RemoveRange(reminders);

            var res = await _dbContext.SaveChangesAsync();
            if (res == -1)
                return;

            _logger.LogInformation("Removed {reminders} reminders from DB", remCount);
        }
    }
}
