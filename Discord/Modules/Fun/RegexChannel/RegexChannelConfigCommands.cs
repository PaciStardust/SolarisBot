using Discord.Interactions;
using Discord;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using System;

namespace SolarisBot.Discord.Modules.Fun.RegexChannel
{
    [Module("fun/regex"), Group("cfg-regex", "[MANAGE CHANNELS ONLY] RegEx channel config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageChannels), RequireUserPermission(GuildPermission.ManageChannels)] //todo: [FEATURE] Info commands
    public sealed class RegexChannelConfigCommands : SolarisInteractionModuleBase
    {
        private readonly RegexChannelService _rcService;

        internal RegexChannelConfigCommands(RegexChannelService rcService)
        {
            _rcService = rcService;
        }

        //todo: [FEATURE] service
        [SlashCommand("add", "Add a RegEx channel")]
        public async Task ConfigureRegexChannelAsync
        (
            [Summary(description: "[Opt] Target channel")] IChannel? channel = null,
            [Summary(description: "[Opt] RegEx to enforce (None to disable)")] string regex = "",
            [Summary(description: "[Opt] Role to apply as punishment")] IRole? punishmentRole = null,
            [Summary(description: "[Opt] Message to send on fail")] string punishmentMsg = "",
            [Summary(description: "[Opt] Timeout duration on fail")] string punishmentTimeout = "0",
            [Summary(description: "[Opt] Delete fail message")] bool deleteMsg = false
        )
        {
            if (!ulong.TryParse(punishmentTimeout, out var parsedPunishmentTimeout))
            {
                await Interaction.ReplyInvalidParameterErrorAsync("punishment timeout");
                return;
            }

            var targetChannel = channel ?? Context.Channel;
            var res = await _rcService.AddRegexChannel(targetChannel, Context.Guild, regex, punishmentRole, punishmentMsg, deleteMsg, parsedPunishmentTimeout);

            await res.Match(
                success => Interaction.ReplyAsync($"RegEx for **<#{success.Value.ChannelId}>** created\n\nRegex: **{regex}**\nRole: **{(punishmentRole is null ? "None" : $"{punishmentRole.Mention}")}**\nMessage: **{(string.IsNullOrWhiteSpace(success.Value.PunishmentMessage) ? "None" : $"\"{success.Value.PunishmentMessage}\"")}**\nTimeout: **{success.Value.PunishmentTimeout}**\nDelete: **{(success.Value.PunishmentDelete ? "Yes" : "No")}**"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("list", "List all RegEx channels")]
        public async Task ListRegexChannelsAsync()
        {
            using var dbCtx = _dbService.GetContext();

            var regexChannels = await dbCtx.RegexChannels.ForGuild(Context.Guild.Id).ToArrayAsync();
            if (regexChannels.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var responseText = string.Join("\n", regexChannels.Select(x => $"- {x.RegexChannelId}: {x.Regex} in <#{x.ChannelId}>"));
            await Interaction.ReplyAsync($"RegEx channels for this guild", responseText); //tpdo: [REFACTOR] Investigate extra newline?
        }

        [SlashCommand("remove", "Remove RegEx channels")]
        //todo: [FEATURE] Channel ID option to avoid leftovers
        public async Task DeleteRegexChannelsAsync
        (
            [Summary(description: "[Opt] Id to delete")] string? targetId = null
        )
        {
            var parsedTargetId = Utils.ToUlongOrNull(targetId);
            if (targetId is not null && parsedTargetId is null)
            {
                await Interaction.ReplyInvalidParameterErrorAsync("target ID");
                return;
            }

            using var dbCtx = _dbService.GetContext();

            var query = dbCtx.RegexChannels.ForGuild(Context.Guild.Id);
            query = parsedTargetId is null
                ? query.ForChannel(Context.Channel.Id)
                : query.Where(x => x.RegexChannelId == parsedTargetId);

            var regexChannels = await query.ToArrayAsync();
            if (regexChannels.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            dbCtx.RegexChannels.RemoveRange(regexChannels);
            _logger.LogDebug("{intTag} Removing {channelCount} regex channels in guild {guild}", GetIntTag(), regexChannels.Length, Context.Guild.Log());
            await dbCtx.SaveChangesAsync();
            _logger.LogInformation("{intTag} Removed {channelCount} regex channels in guild {guild}", GetIntTag(), regexChannels.Length, Context.Guild.Log());
            await Interaction.ReplyAsync($"Removed **{regexChannels.Length}** RegEx channel{(regexChannels.Length == 1 ? string.Empty : "s")}");
        }
    }
}
