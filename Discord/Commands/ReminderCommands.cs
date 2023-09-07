using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;

namespace SolarisBot.Discord.Commands
{
    [Group("reminder", "Manage Reminders"), RequireContext(ContextType.Guild)]
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

        [SlashCommand("config", "[ADMIN ONLY] Enable reminders"), DefaultMemberPermissions(GuildPermission.Administrator)]
        public async Task EnableRemindersAsync(bool enabled)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.RemindersOn = enabled;

            _logger.LogDebug("Setting reminders to {enabled} in guild {guild}", enabled, Context.Guild.Log());
            if (await _dbContext.SaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Set reminders to {enabled} in guild {guild}", enabled, Context.Guild.Log());
            await RespondEmbedAsync("Reminders Configured", $"Reminders are currently **{(enabled ? "enabled" : "disabled")}**");
        }

        [SlashCommand("create", "Create a reminder")]
        public async Task CreateReminderAsync(string text, ulong days = 0, [MaxValue(23)] byte hours = 0, [MaxValue(59)] byte minutes = 0)
        {
            if (days == 0 && hours == 0 && minutes == 0)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput, isEphemeral: true);
                return;
            }

            var dbGuild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
            if (dbGuild == null || !dbGuild.RemindersOn)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden, isEphemeral: true);
                return;
            }

            var reminderCount = dbGuild.Reminders.Select(x => x.UserId == Context.User.Id).Count();
            if (reminderCount >= _botConfig.MaxRemindersPerUser)
            {
                await RespondErrorEmbedAsync("Maximum Reminders", $"Reached maximum reminder count of **{_botConfig.MaxRemindersPerUser}**", isEphemeral: true);
                return;
            }

            var reminderTime = Utils.LongToUlong(DateTimeOffset.Now.AddDays(days).AddHours(hours).AddMinutes(minutes).ToUnixTimeSeconds(), _logger);
            var dbReminder = new DbReminder()
            {
                ChannelId = Context.Channel.Id,
                GId = Context.Guild.Id,
                Text = text,
                Time = reminderTime,
                UserId = Context.User.Id,
                Created = Utils.GetCurrentUnix()
            };

            _logger.LogDebug("Creating reminder {reminder} for user {user} in channel {channel} in guild {guild}", dbReminder, Context.User.Log(), Context.Channel.Log(), Context.Guild.Log());
            dbGuild.Reminders.Add(dbReminder);
            var res = await _dbContext.SaveChangesAsync();
            if (res == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError, isEphemeral: true);
                return;
            }
            _logger.LogInformation("Created reminder {reminder} for user {user} in channel {channel} in guild {guild}", dbReminder, Context.User.Log(), Context.Channel.Log(), Context.Guild.Log());

            await RespondEmbedAsync("Reminder Created", $"{text}\n*(Reminding <t:{reminderTime}:f>)*");
        }

        [SlashCommand("list", "List your reminders")]
        public async Task ListRemindersAsync()
        {
            var reminders = _dbContext.Reminders.Where(x => x.UserId == Context.User.Id);

            if (!reminders.Any())
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            var reminderText = string.Join("\n", reminders.Select(x => $"- [{x.RId}] {x.Text} *(<t:{x.Time}:f>)*"));
            await RespondEmbedAsync("Your Reminders", reminderText, isEphemeral: true);
        }

        [SlashCommand("delete", "Delete a reminder")] 
        public async Task DeleteReminder(ulong id)
        {
            var reminder = _dbContext.Reminders.Where(x => x.RId == id && x.UserId == Context.User.Id).FirstOrDefault();
            if (reminder == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            _logger.LogDebug("Deleting reminder {reminder} from user {user} in DB", reminder, Context.User.Log());
            _dbContext.Reminders.Remove(reminder);
            var res = await _dbContext.TrySaveChangesAsync();
            if (res == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError, isEphemeral: true);
                return;
            }
            _logger.LogInformation("Deleted reminder {reminder} from user {user} in DB", reminder, Context.User.Log());

            await RespondEmbedAsync("Reminder Deleted", $"Deleted reminder: {reminder.Text}", isEphemeral: true);
        }
    }
}
