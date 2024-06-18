using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Reminders
{
    [Module("reminders"), Group("cfg-reminders", "[MANAGE MESSAGES ONLY] Reminders config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageMessages), RequireUserPermission(GuildPermission.ManageMessages)]
    public sealed class ReminderConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ReminderService _reminderService;
        internal ReminderConfigCommands(ReminderService reminderService)
        {
            _reminderService = reminderService;
        }

        [SlashCommand("config", "Enable reminders")]
        public async Task EnableRemindersAsync
        (
            [Summary(description: "Is feature enabled?")] bool enabled
        )
        {
            var res = await _reminderService.ConfigRemindersAsync(Context.Guild, enabled);
            await res.Match(
                success => Interaction.ReplyAsync($"Reminders are currently **{(enabled ? "enabled" : "disabled")}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("wipe", "Wipe reminders")]
        public async Task WipeRemindersAsync
        (
            [Summary(description: "[Opt] Channel to wipe reminders from")] IChannel? channel = null
        )
        {
            var res = await _reminderService.WipeRemindersAsync(Context.Guild, channel);
            await res.Match(
                success => Interaction.ReplyAsync($"Wiped **{success.Value.Length}** reminders from database"),
                none => Interaction.ReplyErrorAsync(GenericError.NoResults),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
