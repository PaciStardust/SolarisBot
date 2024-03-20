using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Modules.Roles;

namespace SolarisBot.Discord.Modules.Bridges
{
    [Module("bridges"), Group("cfg-bridges", "[MANAGE CHANNELS ONLY] Bridge config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageChannels), RequireUserPermission(GuildPermission.ManageChannels)]
    internal class BridgeConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<BridgeConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        private readonly BotConfig _config;
        private readonly DiscordSocketClient _client;

        internal BridgeConfigCommands(ILogger<BridgeConfigCommands> logger, DatabaseContext dbctx, BotConfig config, DiscordSocketClient client)
        {
            _dbContext = dbctx;
            _logger = logger;
            _config = config;
            _client = client;
        }

        [SlashCommand("list", "List all bridges")]
        public async Task ListBridgesAsync
        (
            [Summary(description: "[Opt] List guild bridges")] bool guild = false
        )
        {
            var query = guild
                ? _dbContext.Bridges.ForGuild(Context.Guild.Id)
                : _dbContext.Bridges.ForChannel(Context.Channel.Id);

            var bridges = await query.ToArrayAsync();
            if (bridges.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            string bridgeText = string.Join("\n", bridges.Select(x => $"- {x.BridgeId}: {x.Name} {(Context.Channel.Id == x.ChannelAId ? x.ChannelBId : x.ChannelAId)} in {(Context.Guild.Id == x.GuildAId ? x.GuildBId : x.GuildAId)}"));
            await Interaction.ReplyAsync($"Bridges for this {(guild ? "Guild" : "Channel")}", bridgeText);
        }

        [SlashCommand("create", "Create a bridge")]
        public async Task CreateBridgeAsync
        (
            [MinLength(2), MaxLength(20), Summary(description: "Bridge name")] string name,
            [Summary(description: "Id of target guild")] string guildId,
            [Summary(description: "Id of target channel")] string channelId
        )
        {
            if (!ulong.TryParse(guildId, out var pGuild))
            {
                await Interaction.ReplyErrorAsync("Could not parse guild id");
                return;
            }
            if (!ulong.TryParse(channelId, out var pChannel))
            {
                await Interaction.ReplyErrorAsync("Could not parse channel id");
                return;
            }

            var nameTrimmed = name.Trim();
            if (!DiscordUtils.IsIdentifierValid(nameTrimmed))
            {
                await Interaction.RespondInvalidIdentifierErrorEmbedAsync(nameTrimmed);
                return;
            }

            if (pChannel == Context.Channel.Id)
            {
                await Interaction.ReplyErrorAsync("Can not create a bridge to same channel");
                return;
            }

            var bridgesHere = await _dbContext.Bridges.ForGuild(Context.Guild.Id).CountAsync();
            if (bridgesHere > _config.MaxBridgesPerGuild)
            {
                await Interaction.ReplyErrorAsync($"This guild already has the maximum amount of bridges ({_config.MaxBridgesPerGuild})");
                return;
            }

            var bridgesThere = await _dbContext.Bridges.ForGuild(pChannel).CountAsync();
            if (bridgesThere > _config.MaxBridgesPerGuild)
            {
                await Interaction.ReplyErrorAsync($"Target guild already has the maximum amount of bridges ({_config.MaxBridgesPerGuild})");
                return;
            }

            var duplicate = await _dbContext.Bridges.ForGuild(pGuild).ForGuild(Context.Guild.Id).FirstOrDefaultAsync();
            if (duplicate is not null)
            {
                await Interaction.ReplyErrorAsync("This bridge already exists");
                return;
            }

            var otherGuild = await Context.Client.GetGuildAsync(pGuild);
            if (otherGuild is null)
            {
                await Interaction.ReplyErrorAsync($"Guild with Id {pGuild} could not be found by bot");
                return;
            }

            var otherChannel = await otherGuild.GetChannelAsync(pChannel);
            if (otherChannel is null)
            {
                await Interaction.ReplyErrorAsync($"Channel with Id {pChannel} could not be found in guild by bot");
                return;
            }

            var user = await otherGuild.GetUserAsync(Context.User.Id);
            if (user is null)
            {
                await Interaction.ReplyErrorAsync("You are not in the target guild");
                return;
            }
            if (!user.GetPermissions(otherChannel).ManageChannel)
            {
                await Interaction.ReplyErrorAsync("You do not have the \"Manage Channel\" permission in the target channel");
                return;
            }

            var botUserPerms = (await otherGuild.GetCurrentUserAsync()).GetPermissions(otherChannel);
            if (!botUserPerms.ManageChannel)
            {
                await Interaction.ReplyErrorAsync("Bot does not have the \"Manage Channel\" permission in the target channel");
                return;
            }
            if (!botUserPerms.SendMessages)
            {
                await Interaction.ReplyErrorAsync("Bot does not have the \"Send Messages\" permission in the target channel");
                return;
            }

            var dbBridge = new DbBridge()
            {
                Name = nameTrimmed,
                GuildAId = Context.Guild.Id,
                ChannelAId = Context.Channel.Id,
                GuildBId = otherGuild.Id,
                ChannelBId = otherChannel.Id
            };
            _dbContext.Bridges.Add(dbBridge);
            _logger.LogDebug("{intTag} Adding bridge {bridge} between channel {channel} in guild {guild} and channel {otherChannel} in guild {otherGuild}", GetIntTag(), dbBridge, Context.Channel.Log(), Context.Guild.Log(), otherChannel.Log(), otherGuild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Added bridge {bridge} between channel {channel} in guild {guild} and channel {otherChannel} in guild {otherGuild}", GetIntTag(), dbBridge, Context.Channel.Log(), Context.Guild.Log(), otherChannel.Log(), otherGuild.Log());
            await ((IMessageChannel)otherChannel).SendMessageAsync(embed: EmbedFactory.Default($"{user.Mention} created bridge {dbBridge.ToDiscordInfoString()} channel {Context.Channel.ToDiscordInfoString()} in guild {Context.Guild.ToDiscordInfoString()}"));
            await Interaction.ReplyAsync($"Created bridge {dbBridge.ToDiscordInfoString()} to channel {otherChannel.ToDiscordInfoString()} in guild {otherGuild.ToDiscordInfoString()}");
        }

        [SlashCommand("remove", "Remove bridges from channel")]
        public async Task RemoveBridgeAsync
        (
            [Summary(description: "[Opt] Bridge Id")] string bridgeId = "0"
        )
        {
            if (!ulong.TryParse(bridgeId, out var pBridge))
            {
                await Interaction.ReplyErrorAsync("Could not parse bridge id");
                return;
            }

            var query = _dbContext.Bridges.ForGuild(Context.Guild.Id);
            query = pBridge == ulong.MinValue
                ? query.ForChannel(Context.Channel.Id)
                : query.Where(x => x.BridgeId == pBridge);

            var bridges = await query.ToListAsync();
            if (bridges.Count == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _dbContext.Bridges.RemoveRange(bridges);
            _logger.LogDebug("{intTag} Removing {bridgeCount} bridges in guild {guild}", GetIntTag(), bridges.Count, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Removed {bridgeCount} bridges in guild {guild}", GetIntTag(), bridges.Count, Context.Guild.Log());
            await Interaction.ReplyAsync($"Removed **{bridges.Count}** bridge{(bridges.Count == 0 ? string.Empty : "s")}");

            foreach (var bridge in bridges)
            {
                var channelA = await _client.GetChannelAsync(bridge.ChannelAId);
                var channelB = await _client.GetChannelAsync(bridge.ChannelBId);

                if (channelA is not null && channelA is IMessageChannel msgChannelA)
                    await BridgeHelper.TryNotifyChannelForBridgeDeletionAsync(msgChannelA, channelB, bridge, _logger, true);
                if (channelB is not null && channelB is IMessageChannel msgChannelB)
                    await BridgeHelper.TryNotifyChannelForBridgeDeletionAsync(msgChannelB, channelA, bridge, _logger, false);
            }
        }
    }
}
