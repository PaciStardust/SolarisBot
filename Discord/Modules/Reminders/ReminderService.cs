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
    [Module("reminders"), AutoLoad]
    internal sealed class ReminderService : IHostedService
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
            await RemindUsersAsync();
            _timer.Start();
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

        private async Task RemindUsersAsync() //todo: [REFACTOR] Find a better way to query reminders
        {
            if (_client.LoginState != LoginState.LoggedIn)
                return;

            var nowUnix = Utils.GetCurrentUnix(_logger);
            _logger.LogDebug("Checking Database for reminders");
            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var reminders = await dbCtx.Reminders.FromSqlRaw($"SELECT * FROM reminders WHERE time <= {nowUnix}").ToArrayAsync(); //UInt equality not supported
            if (reminders.Length == 0)
            {
                _logger.LogDebug("Checked database for reminders, none found");
                return;
            }

            _logger.LogDebug("Sending out {reminders} reminders", reminders.Length);
            foreach (var reminder in reminders)
            {
                try
                {
                    var channel = await _client.GetChannelAsync(reminder.ChannelId);
                    if (channel is not null && channel is IMessageChannel msgChannel)
                    {
                        _logger.LogDebug("Reminding user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GuildId);
                        var embed = EmbedFactory.Default($"**{reminder.Text}**\n*(Created <t:{reminder.Created}:f>)*");
                        await msgChannel.SendMessageAsync($"Here is your reminder <@{reminder.UserId}>!", embed: embed);
                        _logger.LogInformation("Reminded user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GuildId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed reminding user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GuildId);
                }
            }

            _logger.LogInformation("Reminders finished, removing {reminders} reminders from DB", reminders.Length);
            dbCtx.Reminders.RemoveRange(reminders);

            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
                _logger.LogError(err, "Failed to remove {reminders} reminders from DB", reminders.Length);
            else
                _logger.LogInformation("Removed {reminders} reminders from DB", reminders.Length);
        }
        #endregion
    }
}
