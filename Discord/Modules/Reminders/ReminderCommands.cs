using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Reminders
{
    [Module("reminders"), Group("reminders", "Manage Reminders"), RequireContext(ContextType.Guild)]
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

        #region Create
        [SlashCommand("create-ts", "Create a reminder using a timestamp")]
        private async Task CreateReminderAsync(string text, ulong timestamp)
        {
            var currentUnix = Utils.GetCurrentUnix();
            if (timestamp < currentUnix)
            {
                await Interaction.ReplyErrorAsync("Timestamp should not be in past");
                return;
            }
            if (timestamp > currentUnix + _botConfig.MaxReminderTimeOffset)
            {
                await Interaction.ReplyErrorAsync("Timestamp too far in the future");
                return;
            }

            var dbGuild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
            if (dbGuild is null || !dbGuild.RemindersOn)
            {
                await Interaction.ReplyErrorAsync("Reminders are not enabled in this guild");
                return;
            }

            var userReminders = await _dbContext.Reminders.ForUser(Context.User.Id).ToListAsync();
            if (userReminders.Count >= _botConfig.MaxRemindersPerUser)
            {
                await Interaction.ReplyErrorAsync($"Reached maximum reminder count of **{_botConfig.MaxRemindersPerUser}**");
                return;
            }
            else if (userReminders.Any(x => x.GuildId == Context.Guild.Id && x.Text == text))
            {
                await Interaction.ReplyErrorAsync("Reminder with this text has already been created in this guild");
                return;
            }

            var dbReminder = new DbReminder()
            {
                ChannelId = Context.Channel.Id,
                GuildId = Context.Guild.Id,
                Text = text,
                Time = timestamp,
                UserId = Context.User.Id,
                Created = currentUnix
            };

            _logger.LogDebug("{intTag} Creating reminder {reminder} for user {user} in channel {channel} in guild {guild}", GetIntTag(), dbReminder, Context.User.Log(), Context.Channel.Log(), Context.Guild.Log());
            _dbContext.Reminders.Add(dbReminder);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Created reminder {reminder} for user {user} in channel {channel} in guild {guild}", GetIntTag(), dbReminder, Context.User.Log(), Context.Channel.Log(), Context.Guild.Log());

            await Interaction.ReplyAsync($"Reminder #{dbReminder.ReminderId}: **{text}**\n*(Reminding <t:{timestamp}:f>)*");
        }

        [SlashCommand("create-in", "Create a reminder in x time")]
        public async Task CreateReminderInAsync(string text, ushort days = 0, [MaxValue(23)] byte hours = 0, [MaxValue(59)] byte minutes = 0)
        {
            if (days == 0 && hours == 0 && minutes == 0)
            {
                await Interaction.ReplyErrorAsync("Time values can not be zero");
                return;
            }

            var offset = DateTimeOffset.Now.AddDays(days).AddHours(hours).AddMinutes(minutes);
            var reminderTime = offset.ToUnixTimeSeconds().LongToUlong(_logger);
            await CreateReminderAsync(text, reminderTime);
        }
        #endregion

        #region Other
        [SlashCommand("list", "List your reminders")]
        public async Task ListRemindersAsync()
        {
            var reminders = await _dbContext.Reminders.ForUser(Context.User.Id).ToArrayAsync();

            if (reminders.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var reminderText = string.Join("\n", reminders.Select(x => $"- [{x.ReminderId}] {x.Text} *(<t:{x.Time}:f>)*"));
            await Interaction.ReplyAsync("Your Reminders", reminderText, isEphemeral: true);
        }

        [SlashCommand("delete", "Delete a reminder")]
        public async Task DeleteReminder(ulong id)
        {
            var reminder = await _dbContext.Reminders.ForUser(Context.User.Id).FirstOrDefaultAsync(x => x.ReminderId == id);
            if (reminder is null)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _logger.LogDebug("{intTag} Deleting reminder {reminder} from user {user} in DB", GetIntTag(), reminder, Context.User.Log());
            _dbContext.Reminders.Remove(reminder);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Deleted reminder {reminder} from user {user} in DB", GetIntTag(), reminder, Context.User.Log());
            await Interaction.ReplyAsync($"Deleted reminder #{reminder.ReminderId}", isEphemeral: true);
        }
        #endregion
    }
}
