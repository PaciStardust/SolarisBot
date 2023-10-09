using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;

namespace SolarisBot.Discord.Commands
{
    [Group("reminders", "Manage Reminders"), RequireContext(ContextType.Guild)]
    public sealed class ReminderCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<ReminderCommands> _logger;
        private readonly DatabaseContext _dbContext;
        private readonly BotConfig _botConfig;
        internal ReminderCommands(ILogger<ReminderCommands> logger, DatabaseContext dbctx, BotConfig botConfig)
        {
            _dbContext = dbctx;
            _logger = logger;
            _botConfig = botConfig;
        }
        protected override ILogger? GetLogger() => _logger;

        [SlashCommand("config", "[MANAGE GUILD ONLY] Enable reminders"), DefaultMemberPermissions(GuildPermission.ManageGuild), RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task EnableRemindersAsync(bool enabled)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.RemindersOn = enabled;

            _logger.LogDebug("{intTag} Setting reminders to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
            var (_, err) = await _dbContext.TrySaveChangesAsync();
            if (err != null)
            {
                _logger.LogError(err, "{intTag} Failed to set reminders to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
                await RespondErrorEmbedAsync(err);
                return;
            }
            _logger.LogInformation("{intTag} Set reminders to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
            await RespondEmbedAsync("Reminders Configured", $"Reminders are currently **{(enabled ? "enabled" : "disabled")}**");
        }

        [SlashCommand("wipe", "[MANAGE MESSAGES ONLY] Wipe reminders"), DefaultMemberPermissions(GuildPermission.ManageMessages), RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task WipeRemindersAsync(IChannel? channel = null)
        {
            var query = _dbContext.Reminders.Where(x => x.GuildId == Context.Guild.Id);
            if (channel != null)
                query.Where(x => x.ChannelId == channel.Id);

            var reminders = await query.ToArrayAsync();
            if (reminders.Length == 0)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                return;
            }

            _logger.LogDebug("{intTag} Wiping {reminders} reminders from guild {guild}", GetIntTag(), reminders.Length, Context.Guild.Log());
            _dbContext.Reminders.RemoveRange(reminders);
            var (_, err) = await _dbContext.TrySaveChangesAsync();
            if (err != null)
            {
                _logger.LogError(err, "{intTag} Failed to wipe {reminders} reminders from guild {guild}", GetIntTag(), reminders.Length, Context.Guild.Log());
                await RespondErrorEmbedAsync(err);
                return;
            }
            _logger.LogInformation("{intTag} Wiped {reminders} reminders from guild {guild}", GetIntTag(), reminders.Length, Context.Guild.Log());
            await RespondEmbedAsync("Reminders Wiped", $"Wiped **{reminders.Length}** reminders from database");
        }

        [SlashCommand("create", "Create a reminder")] //todo: [FEATURE] Creation w timestamp? DateTime Timezone?
        public async Task CreateReminderAsync(string text, ulong days = 0, [MaxValue(23)] byte hours = 0, [MaxValue(59)] byte minutes = 0)
        {
            if (days == 0 && hours == 0 && minutes == 0)
            {
                await RespondInvalidInputErrorEmbedAsync("Time values can not be zero");
                return;
            }

            var dbGuild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
            if (dbGuild == null || !dbGuild.RemindersOn)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden);
                return;
            }

            var userReminders = await _dbContext.Reminders.Where(x => x.UserId == Context.User.Id).ToListAsync();
            if (userReminders.Count >= _botConfig.MaxRemindersPerUser)
            {
                await RespondErrorEmbedAsync("Maximum Reminders", $"Reached maximum reminder count of **{_botConfig.MaxRemindersPerUser}**");
                return;
            }
            else if (userReminders.Any(x => x.GuildId == Context.Guild.Id && x.Text == text))
            {
                await RespondErrorEmbedAsync("Duplicate Reminder", "Reminder with this name has already been created");
                return;
            }

            var offset = DateTimeOffset.Now.AddDays(days).AddHours(hours).AddMinutes(minutes);
            var reminderTime = Utils.LongToUlong(offset.AddSeconds(-offset.Second).ToUnixTimeSeconds(), _logger);
            var dbReminder = new DbReminder()
            {
                ChannelId = Context.Channel.Id,
                GuildId = Context.Guild.Id,
                Text = text,
                Time = reminderTime,
                UserId = Context.User.Id,
                Created = Utils.GetCurrentUnix()
            };

            _logger.LogDebug("{intTag} Creating reminder {reminder} for user {user} in channel {channel} in guild {guild}", GetIntTag(), dbReminder, Context.User.Log(), Context.Channel.Log(), Context.Guild.Log());
            dbGuild.Reminders.Add(dbReminder);
            var (_, err) = await _dbContext.TrySaveChangesAsync();
            if (err != null)
            {
                _logger.LogError(err, "{intTag} Failed to create reminder {reminder} for user {user} in channel {channel} in guild {guild}", GetIntTag(), dbReminder, Context.User.Log(), Context.Channel.Log(), Context.Guild.Log());
                await RespondErrorEmbedAsync(err);
                return;
            }
            _logger.LogInformation("{intTag} Created reminder {reminder} for user {user} in channel {channel} in guild {guild}", GetIntTag(), dbReminder, Context.User.Log(), Context.Channel.Log(), Context.Guild.Log());

            await RespondEmbedAsync($"Reminder #{dbReminder.ReminderId} Created", $"**{text}**\n*(Reminding <t:{reminderTime}:f>)*");
        }

        [SlashCommand("list", "List your reminders")]
        public async Task ListRemindersAsync()
        {
            var reminders = await _dbContext.Reminders.Where(x => x.UserId == Context.User.Id).ToArrayAsync();

            if (reminders.Length == 0)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                return;
            }

            var reminderText = string.Join("\n", reminders.Select(x => $"- [{x.ReminderId}] {x.Text} *(<t:{x.Time}:f>)*"));
            await RespondEmbedAsync("Your Reminders", reminderText, isEphemeral: true);
        }

        [SlashCommand("delete", "Delete a reminder")]
        public async Task DeleteReminder(ulong id)
        {
            var reminder = await _dbContext.Reminders.FirstOrDefaultAsync(x => x.ReminderId == id && x.UserId == Context.User.Id);
            if (reminder == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                return;
            }

            _logger.LogDebug("{intTag} Deleting reminder {reminder} from user {user} in DB", GetIntTag(), reminder, Context.User.Log());
            _dbContext.Reminders.Remove(reminder);
            var (_, err) = await _dbContext.TrySaveChangesAsync();
            if (err != null)
            {
                _logger.LogError(err, "{intTag} Failed to delete reminder {reminder} from user {user} in DB", GetIntTag(), reminder, Context.User.Log());
                await RespondErrorEmbedAsync(err);
                return;
            }
            _logger.LogInformation("{intTag} Deleted reminder {reminder} from user {user} in DB", GetIntTag(), reminder, Context.User.Log());

            await RespondEmbedAsync("Reminder Deleted", $"Deleted reminder: {reminder.Text}", isEphemeral: true);
        }
    }
}
