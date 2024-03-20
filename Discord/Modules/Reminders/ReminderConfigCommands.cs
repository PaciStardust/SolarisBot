using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Reminders
{
    [Module("reminders"), Group("cfg-reminders", "[MANAGE MESSAGES ONLY] Reminders config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageMessages), RequireUserPermission(GuildPermission.ManageMessages)]
    public sealed class ReminderConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<ReminderConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal ReminderConfigCommands(ILogger<ReminderConfigCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("config", "Enable reminders")]
        public async Task EnableRemindersAsync
        (
            [Summary(description: "Is feature enabled?")] bool enabled
        )
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.RemindersOn = enabled;

            _logger.LogDebug("{intTag} Setting reminders to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set reminders to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
            await Interaction.ReplyAsync($"Reminders are currently **{(enabled ? "enabled" : "disabled")}**");
        }

        [SlashCommand("wipe", "Wipe reminders")]
        public async Task WipeRemindersAsync
        (
            [Summary(description: "[Opt] Channel to wipe reminders from")] IChannel? channel = null
        )
        {
            var query = _dbContext.Reminders.ForGuild(Context.Guild.Id);
            if (channel is not null)
                query.ForChannel(channel.Id);

            var reminders = await query.ToArrayAsync();
            if (reminders.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _logger.LogDebug("{intTag} Wiping {reminders} reminders from guild {guild}", GetIntTag(), reminders.Length, Context.Guild.Log());
            _dbContext.Reminders.RemoveRange(reminders);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Wiped {reminders} reminders from guild {guild}", GetIntTag(), reminders.Length, Context.Guild.Log());
            await Interaction.ReplyAsync($"Wiped **{reminders.Length}** reminders from database");
        }
    }
}
