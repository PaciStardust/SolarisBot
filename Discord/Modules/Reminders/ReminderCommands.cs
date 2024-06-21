using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Reminders
{
    [Module("reminders"), Group("reminders", "Manage Reminders"), RequireContext(ContextType.Guild)]
    public sealed class ReminderCommands : SolarisInteractionModuleBase
    {
        private readonly ReminderService _reminderService;

        internal ReminderCommands(ReminderService reminderService)
        {
            _reminderService = reminderService;
        }

        #region Create
        [SlashCommand("create-ts", "Create a reminder using a timestamp")]
        public async Task CreateReminderAsync
        (
            [Summary(description: "Reminder text")] string text,
            [Summary(description: "Hammertime/Unix timestamp for reminder")] string timestamp
        )
        {
            if (!ulong.TryParse(timestamp, out var parsedTimestamp))
            {
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("timestamp"));
                return;
            }

            var res = await _reminderService.CreateReminderUnixAsync(Context.Guild, Context.Channel, Context.User, text, parsedTimestamp);
            await res.Match(
                success => Interaction.ReplyAsync($"Reminder #{success.Value.ReminderId}: **{text}**\n*(Reminding <t:{parsedTimestamp}:f>)*"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("create-in", "Create a reminder in x time")]
        public async Task CreateReminderInAsync
        (
            [Summary(description: "Reminder text")] string text, 
            [Summary(description: "[Opt] Days to remind in")] ushort days = 0, 
            [Summary(description: "[Opt] Hours to remind in"), MaxValue(23)] byte hours = 0,
            [Summary(description: "[Opt] Minutes to remind in"), MaxValue(59)] byte minutes = 0
        )
        {
            var res = await _reminderService.CreateReminderInAsync(Context.Guild, Context.Channel, Context.User, text, days, minutes, hours);
            await res.Match(
                success => Interaction.ReplyAsync($"Reminder #{success.Value.ReminderId}: **{text}**\n*(Reminding <t:{success.Value.RemindAt}:f>)*"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
        #endregion

        #region Other
        [SlashCommand("list", "List your reminders")]
        public async Task ListRemindersAsync()
        {
            var reminders = await _reminderService.GetRemindersForUserAsync(Context.User.Id);
            if (reminders.Length == 0)
            {
                await Interaction.ReplyErrorAsync(StandardError.NoResults);
                return;
            }

            var reminderText = string.Join("\n", reminders.Select(x => $"- [{x.ReminderId}] {x.Text} *(<t:{x.RemindAt}:f>)*"));
            await Interaction.ReplyAsync("Your Reminders", reminderText, isEphemeral: true);
        }

        [SlashCommand("delete", "Delete a reminder")]
        public async Task DeleteReminderAsync
        (
            [Summary(description: "Id of reminder")] string reminderId
        )
        {
            if (!ulong.TryParse(reminderId, out var parsedReminderId))
            {
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("reminder ID"));
                return;
            }

            var res = await _reminderService.DeleteReminderAsync(Context.User, parsedReminderId);
            await res.Match(
                success => Interaction.ReplyAsync($"Deleted reminder #{success.Value.ReminderId}", isEphemeral: true),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
        #endregion
    }
}
