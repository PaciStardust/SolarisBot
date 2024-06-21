using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Bridges
{
    [Module("bridges"), Group("cfg-bridges", "[MANAGE CHANNELS ONLY] Bridge config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageChannels), RequireUserPermission(GuildPermission.ManageChannels)]
    internal class BridgeConfigCommands : SolarisInteractionModuleBase
    {
        private readonly BridgeService _bridgeService;
        internal BridgeConfigCommands(BridgeService bridgeService)
        {
            _bridgeService = bridgeService;
        }

        [SlashCommand("list", "List all bridges")]
        public async Task ListBridgesAsync
        (
            [Summary(description: "[Opt] Limit search to channel")] bool channelOnly = false
        )
        {
            var idToUse = channelOnly ? Context.Channel.Id : Context.Guild.Id;
            var bridges = await _bridgeService.GetConnectedBridgesAsync(channelOnly, idToUse);

            if (bridges.Length == 0)
            {
                await Interaction.ReplyErrorAsync(StandardError.NoResults);
                return;
            }

            string bridgeText = string.Join("\n", bridges.Select(x => $"- {x.BridgeId}: {x.Name} <#{(Context.Channel.Id == x.ChannelAId ? x.ChannelBId : x.ChannelAId)}> in {(Context.Guild.Id == x.GuildAId ? x.GuildBId : x.GuildAId)}"));
            await Interaction.ReplyAsync($"Bridges for this {(channelOnly ? "Channel" : "Guild")}", bridgeText);
        }

        [SlashCommand("create", "Create a bridge")]
        public async Task CreateBridgeAsync
        (
            [MinLength(2), MaxLength(20), Summary(description: "Bridge name")] string name,
            [Summary(description: "Id of target guild")] string guildId,
            [Summary(description: "Id of target channel")] string channelId
        )
        {
            if (!ulong.TryParse(guildId, out var parsedGuildId) || parsedGuildId == 0)
            {
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("guild ID"));
                return;
            }
            if (!ulong.TryParse(channelId, out var parsedChannelId) || parsedChannelId == 0)
            {
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("channel ID"));
                return;
            }

            //Long interaction, so deffered
            await Interaction.DeferAsync();

            var serviceResult = await _bridgeService.CreateBridgeAsync(name, parsedGuildId, parsedChannelId, Context.Channel.Id, Context.Guild.Id, Context.User.Id);
            await serviceResult.Match(
                success => Interaction.ReplyAsync($"Created bridge {success.Value.ToDiscordInfoString()}"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("remove", "Remove bridges from channel")]
        public async Task RemoveBridgeAsync //todo: [FEATURE] Channel ID option to avoid leftovers
        (
            [Summary(description: "[Opt] Bridge Id")] string? bridgeId = null
        )
        {
            var parsedBridgeId = Utils.ToUlongOrNull(bridgeId);
            if (bridgeId is not null && parsedBridgeId is null)
            {
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("bridge ID"));
                return;
            }

            var serviceResult = await _bridgeService.RemoveBridgesAsync(Context.Guild.Id, Context.Channel.Id, parsedBridgeId);

            await serviceResult.Match(
                success => Interaction.ReplyAsync($"Removed **{success.Value.Length}** bridge{(success.Value.Length == 1 ? string.Empty : "s")}"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
