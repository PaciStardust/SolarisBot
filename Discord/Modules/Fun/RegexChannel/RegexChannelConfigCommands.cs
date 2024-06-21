using Discord.Interactions;
using Discord;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Text.RegularExpressions;

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
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("punishment timeout"));
                return;
            }

            var targetChannel = channel ?? Context.Channel;
            var res = await _rcService.AddRegexChannel(targetChannel, Context.Guild, regex, punishmentRole, punishmentMsg, deleteMsg, parsedPunishmentTimeout);

            await res.Match(
                success => Interaction.ReplyAsync($"RegEx for **<#{success.Value.ChannelId}>** created\n\nRegex: **{success.Value.Regex}**\nRole: **{(punishmentRole is null ? "None" : $"{punishmentRole.Mention}")}**\nMessage: **{(string.IsNullOrWhiteSpace(success.Value.PunishmentMessage) ? "None" : $"\"{success.Value.PunishmentMessage}\"")}**\nTimeout: **{success.Value.PunishmentTimeout}**\nDelete: **{(success.Value.PunishmentDelete ? "Yes" : "No")}**"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("list", "List all RegEx channels")]
        public async Task ListRegexChannelsAsync()
        {
            var regexChannels = await _rcService.GetRegexChannelsAsync(Context.Guild.Id);
            if (regexChannels.Length == 0)
            {
                await Interaction.ReplyErrorAsync(StandardError.NoResults);
                return;
            }

            var responseText = string.Join("\n", regexChannels.Select(x => $"- {x.RegexChannelId}: {x.Regex} in <#{x.ChannelId}>"));
            await Interaction.ReplyAsync($"RegEx channels for this guild", responseText); //tpdo: [REFACTOR] Investigate extra newline?
        }

        [SlashCommand("remove", "Remove RegEx channels")]
        public async Task DeleteRegexChannelsAsync
        (
            [Summary(description: "[Opt] Id to delete")] string? targetId = null
        )
        {
            var parsedTargetId = Utils.ToUlongOrNull(targetId);
            if (targetId is not null && parsedTargetId is null)
            {
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("target ID"));
                return;
            }

            var useChannelId = parsedTargetId is null;
            var usedId = useChannelId ? Context.Channel.Id : parsedTargetId!.Value;
            var res = await _rcService.DeleteRegexChannelAsync(useChannelId, usedId, Context.Guild);

            await res.Match(
                success => Interaction.ReplyAsync($"Removed **{success.Value.Length}** RegEx channel{(success.Value.Length == 1 ? string.Empty : "s")}"),
                error => Interaction.ReplyErrorAsync(StandardError.NoResults),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
