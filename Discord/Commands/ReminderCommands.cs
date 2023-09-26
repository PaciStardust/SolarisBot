﻿using Discord;
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

        [SlashCommand("config", "[ADMIN ONLY] Enable reminders"), DefaultMemberPermissions(GuildPermission.Administrator), RequireUserPermission(GuildPermission.Administrator)]
        public async Task EnableRemindersAsync(bool enabled)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.RemindersOn = enabled;

            _logger.LogDebug("{intTag} Setting reminders to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                _logger.LogWarning("{intTag} Failed to set reminders to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("{intTag} Set reminders to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
            await RespondEmbedAsync("Reminders Configured", $"Reminders are currently **{(enabled ? "enabled" : "disabled")}**");
        }

        [SlashCommand("wipe", "Wipe reminders"), DefaultMemberPermissions(GuildPermission.ManageMessages), RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task WipeRemindersAsync(IChannel? channel = null)
        {
            var query = _dbContext.Reminders.Where(x => x.GId == Context.Guild.Id);
            if (channel != null)
                query.Where(x => x.ChannelId == channel.Id);

            var reminders = await query.ToArrayAsync();
            if (reminders.Length == 0)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            _logger.LogDebug("{intTag} Wiping {reminders} reminders from guild {guild}", GetIntTag(), reminders.Length, Context.Guild.Log());
            _dbContext.Reminders.RemoveRange(reminders);
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                _logger.LogWarning("{intTag} Failed to wipe {reminders} reminders from guild {guild}", GetIntTag(), reminders.Length, Context.Guild.Log());
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError, isEphemeral: true);
                return;
            }
            _logger.LogInformation("{intTag} Wiped {reminders} reminders from guild {guild}", GetIntTag(), reminders.Length, Context.Guild.Log());
            await RespondEmbedAsync("Reminders Wiped", $"Wiped **{reminders.Length}** reminders from database");
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

            var userReminders = await _dbContext.Reminders.Where(x => x.UserId == Context.User.Id).ToListAsync();
            if (userReminders.Count >= _botConfig.MaxRemindersPerUser)
            {
                await RespondErrorEmbedAsync("Maximum Reminders", $"Reached maximum reminder count of **{_botConfig.MaxRemindersPerUser}**", isEphemeral: true);
                return;
            }
            else if (userReminders.Any(x => x.GId == Context.Guild.Id && x.Text == text))
            {
                await RespondErrorEmbedAsync("Duplicate Reminder", "Reminder with this name has already been created", isEphemeral: true);
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

            _logger.LogDebug("{intTag} Creating reminder {reminder} for user {user} in channel {channel} in guild {guild}", GetIntTag(), dbReminder, Context.User.Log(), Context.Channel.Log(), Context.Guild.Log());
            dbGuild.Reminders.Add(dbReminder);
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                _logger.LogWarning("{intTag} Failed to create reminder {reminder} for user {user} in channel {channel} in guild {guild}", GetIntTag(), dbReminder, Context.User.Log(), Context.Channel.Log(), Context.Guild.Log());
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("{intTag} Created reminder {reminder} for user {user} in channel {channel} in guild {guild}", GetIntTag(), dbReminder, Context.User.Log(), Context.Channel.Log(), Context.Guild.Log());

            await RespondEmbedAsync($"Reminder #{dbReminder.RId} Created", $"**{text}**\n*(Reminding <t:{reminderTime}:f>)*");
        }

        [SlashCommand("list", "List your reminders")]
        public async Task ListRemindersAsync()
        {
            var reminders = await _dbContext.Reminders.Where(x => x.UserId == Context.User.Id).ToArrayAsync();

            if (reminders.Length == 0)
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
            var reminder = await _dbContext.Reminders.FirstOrDefaultAsync(x => x.RId == id && x.UserId == Context.User.Id);
            if (reminder == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            _logger.LogDebug("{intTag} Deleting reminder {reminder} from user {user} in DB", GetIntTag(), reminder, Context.User.Log());
            _dbContext.Reminders.Remove(reminder);
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                _logger.LogWarning("{intTag} Failed to delete reminder {reminder} from user {user} in DB", GetIntTag(), reminder, Context.User.Log());
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("{intTag} Deleted reminder {reminder} from user {user} in DB", GetIntTag(), reminder, Context.User.Log());

            await RespondEmbedAsync("Reminder Deleted", $"Deleted reminder: {reminder.Text}", isEphemeral: true);
        }
    }
}
