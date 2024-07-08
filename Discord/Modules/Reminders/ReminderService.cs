using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Timers;

namespace SolarisBot.Discord.Modules.Reminders
{
    [Module("reminders"), AutoLoadService]
    internal sealed class ReminderService
    {
        private readonly ILogger<ReminderService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseService _dbService;
        private readonly System.Timers.Timer _timer;
        private readonly BotConfig _botConfig;

        public ReminderService(ILogger<ReminderService> logger, DiscordSocketClient client, DatabaseService dbService, BotConfig botConfig)
        {
            _client = client;
            _dbService = dbService;
            _logger = logger;
            _botConfig = botConfig;
            _timer = new System.Timers.Timer(TimeSpan.FromSeconds(30));
            _timer.Elapsed += new ElapsedEventHandler(RemindUsersAsync);
            _client.Ready += OnClientReady;
        }

        #region Commands
        /// <summary>
        /// Creates a reminder
        /// </summary>
        /// <param name="guild">Guild of reminder</param>
        /// <param name="channel">Channel of reminder</param>
        /// <param name="user">Reminded user</param>
        /// <param name="text">Text for reminder</param>
        /// <param name="timestamp">Timestamp (Unix)</param>
        /// <returns>Reminder on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbReminder>, Error<string>, Error<Exception>>> CreateReminderUnixAsync(IGuild guild, IChannel channel, IUser user, string text, ulong timestamp)
        {
            var currentUnix = Utils.GetCurrentUnix();
            if (timestamp < currentUnix)
                return new Error<string>("Timestamp should not be in past");
            if (timestamp > currentUnix + _botConfig.MaxReminderTimeOffset)
                return new Error<string>("Timestamp too far in the future");

            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetGuildByIdAsync(guild.Id);
            if (dbGuild is null || !dbGuild.RemindersOn)
                return new Error<string>(StandardError.DisabledFeature("Reminders"));

            var userReminders = await dbCtx.Reminders.ForUser(user.Id).ToArrayAsync();
            if (userReminders.Length >= _botConfig.MaxRemindersPerUser)
                return new Error<string>($"Reached maximum reminder count of **{_botConfig.MaxRemindersPerUser}**");
            if (userReminders.Any(x => x.GuildId == guild.Id && x.Text == text))
                return new Error<string>("Reminder with this text has already been created in this guild");

            var dbReminder = new DbReminder()
            {
                ChannelId = channel.Id,
                GuildId = guild.Id,
                Text = text,
                RemindAt = timestamp,
                UserId = user.Id,
                CreatedAt = currentUnix
            };

            _logger.LogTrace("Creating reminder {reminder} for user {user} in channel {channel} in guild {guild}", dbReminder, user.Log(), channel.Log(), guild.Log());
            dbCtx.Reminders.Add(dbReminder);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed creating reminder {reminder} for user {user} in channel {channel} in guild {guild}", dbReminder, user.Log(), channel.Log(), guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogDebug("Created reminder {reminder} for user {user} in channel {channel} in guild {guild}", dbReminder, user.Log(), channel.Log(), guild.Log());
            return new Success<DbReminder>(dbReminder);
        }

        /// <summary>
        /// Creates a reminder
        /// </summary>
        /// <param name="guild">Guild of reminder</param>
        /// <param name="channel">Channel of reminder</param>
        /// <param name="user">Reminded user</param>
        /// <param name="text">Text for reminder</param>
        /// <param name="days">Days until reminder</param>
        /// <param name="hours">Hours until reminder</param>
        /// <param name="minutes">Minutes until reminder</param>
        /// <returns>Reminder on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbReminder>, Error<string>, Error<Exception>>> CreateReminderInAsync(IGuild guild, IChannel channel, IUser user, string text, ushort days, byte hours, byte minutes)
        {
            if (days == 0 && hours == 0 && minutes == 0)
                return new Error<string>("Time values can not be zero");

            try
            {
                var offset = DateTimeOffset.Now.AddDays(days).AddHours(hours).AddMinutes(minutes);
                var reminderTime = Convert.ToUInt64(offset.ToUnixTimeSeconds());
                return await CreateReminderUnixAsync(guild, channel, user, text, reminderTime);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed converting reminder time");
                return new Error<string>(StandardError.FailedConversion("provided time", "usable format"));
            }
        }

        /// <summary>
        /// Gets all reminders for a user
        /// </summary>
        /// <param name="userId">Id of user</param>
        /// <returns>Reminders as array</returns>
        internal async Task<DbReminder[]> GetRemindersForUserAsync(ulong userId)
        {
            using var dbCtx = _dbService.GetContext();
            var reminders = await dbCtx.Reminders.ForUser(userId).ToArrayAsync();
            return reminders;
        }

        /// <summary>
        /// Deletes a reminder
        /// </summary>
        /// <param name="user">Id of user</param>
        /// <param name="reminderId">Id of reminder</param>
        /// <returns>Deleted reminder on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbReminder>, Error<string>, Error<Exception>>> DeleteReminderAsync(IUser user, ulong reminderId)
        {
            using var dbCtx = _dbService.GetContext();
            var reminder = await dbCtx.Reminders.ForUser(user.Id).FirstOrDefaultAsync(x => x.ReminderId == reminderId);
            if (reminder is null)
                return new Error<string>(StandardError.NoResults);

            _logger.LogTrace("Deleting reminder {reminder} from user {user} in DB", reminder, user.Log());
            dbCtx.Reminders.Remove(reminder);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed deleting reminder {reminder} from user {user} in DB", reminder, user.Log());
                return new Error<Exception>(err);
            }
            _logger.LogDebug("Deleted reminder {reminder} from user {user} in DB", reminder, user.Log());
            return new Success<DbReminder>(reminder);
        }
        #endregion

        #region Commands - Config
        /// <summary>
        /// Configure reminders in a guild
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="enabled">Enabled?</param>
        /// <returns>GuildConfig on success / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigRemindersAsync(IGuild guild, bool enabled)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);
            dbGuild.RemindersOn = enabled;

            _logger.LogTrace("Setting reminders to {enabled} in guild {guild}", enabled, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting reminders to {enabled} in guild {guild}", enabled, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogDebug("Set reminders to {enabled} in guild {guild}", enabled, guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }

        /// <summary>
        /// Wipes reminders in a channel or guild
        /// </summary>
        /// <param name="guild">Id of guild</param>
        /// <param name="channel">Id of channel</param>
        /// <returns>Wiped reminders on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbReminder[]>, Error<string>, Error<Exception>>> WipeRemindersAsync(IGuild guild, IChannel? channel)
        {
            using var dbCtx = _dbService.GetContext();
            var query = dbCtx.Reminders.ForGuild(guild.Id);
            if (channel is not null)
                query.ForChannel(channel.Id);

            var reminders = await query.ToArrayAsync();
            if (reminders.Length == 0)
                return new Error<string>(StandardError.NoResults);

            _logger.LogTrace("Wiping {reminders} reminders from guild {guild}", reminders.Length, guild.Log());
            dbCtx.Reminders.RemoveRange(reminders);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed wiping {reminders} reminders from guild {guild}", reminders.Length, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogDebug("Wiped {reminders} reminders from guild {guild}", reminders.Length, guild.Log());
            return new Success<DbReminder[]>(reminders);
        }
        #endregion

        #region Reminding
        /// <summary>
        /// Starts the timer when the client is ready
        /// </summary>
        /// <returns></returns>
        private Task OnClientReady()
        {
            _logger.LogInformation("Started reminder timer");
            _timer.Start();
            return Task.CompletedTask;
        }

        private async void RemindUsersAsync(object? source, ElapsedEventArgs args)
            => await RemindUsersAsync();

        /// <summary>
        /// Sends out new reminders
        /// </summary>
        private async Task RemindUsersAsync()
        {
            if (_client.LoginState != LoginState.LoggedIn || _client.ConnectionState != ConnectionState.Connected)
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
                _logger.LogDebug("Reminders finished, no reminders to delete");
                return;
            }

            _logger.LogTrace("Reminders finished, removing {reminders} reminders from DB", remindersToDelete.Count);
            dbCtx.Reminders.RemoveRange(remindersToDelete);

            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
                _logger.LogError(err, "Reminders finished, failed to remove {reminders} reminders from DB", remindersToDelete.Count);
            else
                _logger.LogDebug("Reminders finished, removed {reminders} reminders from DB", remindersToDelete.Count);
        }

        /// <summary>
        /// Sends out a reminder
        /// </summary>
        /// <param name="reminder">Reminder to send</param>
        /// <returns>Success?</returns>
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

                _logger.LogTrace("Reminding user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GuildId);
                var embed = EmbedFactory.Default($"**{reminder.Text}**\n*(Created <t:{reminder.CreatedAt}:f>)*");
                await msgChannel.SendMessageAsync($"Here is your reminder <@{reminder.UserId}>!", embed: embed);
                _logger.LogDebug("Reminded user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GuildId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed reminding user {user} in channel {channel} in guild {guild} / Removing from DB", reminder.UserId, reminder.ChannelId, reminder.GuildId);
                return false;
            }

            return true;
        }
        #endregion
    }
}
