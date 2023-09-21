using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceProvider _provider;
        private readonly System.Timers.Timer _timer;

        public ReminderService(ILogger<ReminderService> logger, DiscordSocketClient client, IServiceProvider provider)
        {
            _client = client;
            _provider = provider;
            _logger = logger;
            _timer = new System.Timers.Timer(TimeSpan.FromSeconds(30));
            _timer.Elapsed += new ElapsedEventHandler(RemindUsersAsync);
        }

        #region Start / Stop
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client.UserLeft += RemoveOnUserLeftAsync;
            _client.ChannelDestroyed += RemoveOnChannelDestroyedAsync;
            await RemindUsersAsync();
            _timer.Start();
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _timer.Stop();
            return Task.CompletedTask;
        }
        #endregion

        #region Removal
        private async Task RemoveOnUserLeftAsync(SocketGuild guild, SocketUser user) //todo: [TEST] Do reminders get deleted when user leaves?, move to a CleanupService
        {
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var quotes = await dbCtx.Quotes.Where(x => x.GId == guild.Id && x.CreatorId == user.Id).ToArrayAsync();
            if (quotes.Length == 0)
                return;
            await RemoveQuotesAsync(quotes);
        }

        private async Task RemoveOnChannelDestroyedAsync(SocketChannel channel) //todo: [TEST] Do reminders get deleted on channel destruction?
        {
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var quotes = await dbCtx.Quotes.Where(x => x.ChannelId == channel.Id).ToArrayAsync();
            if (quotes.Length == 0)
                return;
            await RemoveQuotesAsync(quotes);
        }

        private async Task<bool> RemoveQuotesAsync(DbQuote[] quotes)
        {
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            dbCtx.Quotes.RemoveRange(quotes);
            if (await dbCtx.TrySaveChangesAsync() == -1)
            {
                //todo: [LOG] logging
                return false;
            }
            return true;
        }
        #endregion

        #region Reminding
        private async void RemindUsersAsync(object? source, ElapsedEventArgs args)
            => await RemindUsersAsync();

        private async Task RemindUsersAsync()
        {
            if (_client.LoginState != LoginState.LoggedIn)
                return;

            var nowUnix = Utils.GetCurrentUnix(_logger);
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var reminders = await dbCtx.Reminders.FromSqlRaw($"SELECT * FROM reminders WHERE time <= {nowUnix}").ToArrayAsync(); //UInt equality not supported
            if (reminders.Length == 0)
                return;

            _logger.LogDebug("Sending out {reminders} reminders", reminders.Length);
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

            _logger.LogInformation("Reminders finished, removing {reminders} reminders from DB", reminders.Length);
            dbCtx.Reminders.RemoveRange(reminders);

            var res = await dbCtx.SaveChangesAsync();
            if (res == -1)
                return;

            _logger.LogInformation("Removed {reminders} reminders from DB", reminders.Length);
        }
        #endregion
    }
}
