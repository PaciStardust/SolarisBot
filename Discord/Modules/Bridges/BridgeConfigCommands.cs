using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Database.Models;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Bridges
{
    [Module("bridges"), Group("cfg-bridges", "[MANAGE CHANNELS ONLY] Bridge config commands")] //todo: Cleanup, Service, Per server limit
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageChannels), RequireUserPermission(GuildPermission.ManageChannels)]
    internal class BridgeConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<BridgeConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal BridgeConfigCommands(ILogger<BridgeConfigCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("list", "List all bridges")]
        public async Task ListBridgesAsync(bool serverwide = false) //todo: [TESTING] Does listing work?
        {
            var query = serverwide
                ? _dbContext.Bridges.ForGuild(Context.Guild.Id)
                : _dbContext.Bridges.ForChannel(Context.Channel.Id);

            var bridges = await query.ToArrayAsync();
            if (bridges.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            string bridgeText = string.Join("\n", bridges.Select(x => $"- {x.BridgeId}: {x.Name} {(Context.Channel.Id == x.ChannelAId ? x.ChannelBId : x.ChannelAId)} in {(Context.Guild.Id == x.GuildAId ? x.GuildBId : x.GuildAId)}"));
            await Interaction.ReplyAsync($"**Bridges for {(serverwide ? "guild" : "channel")}", bridgeText);
        }

        [SlashCommand("create", "Create a bridge")]
        public async Task CreateBridgeAsync(string name, ulong guildId, ulong channelId) //todo: [TESTING] Does bridge creation work, name limit
        {
            if (channelId == Context.Channel.Id)
            {
                await Interaction.ReplyErrorAsync("Can not create a bridge to same channel");
                return;
            }

            var duplicate = await _dbContext.Bridges.ForGuild(guildId).ForGuild(Context.Guild.Id).FirstOrDefaultAsync();
            if (duplicate is not null)
            {
                await Interaction.ReplyErrorAsync("This bridge already exists");
                return;
            }

            var otherGuild = await Context.Client.GetGuildAsync(guildId);
            if (otherGuild is null)
            {
                await Interaction.ReplyErrorAsync($"Guild with Id {guildId} could not be found by bot");
                return;
            }

            var otherChannel = await otherGuild.GetChannelAsync(channelId);
            if (otherChannel is null)
            {
                await Interaction.ReplyErrorAsync($"Channel with Id {guildId} could not be found in guild by bot");
                return;
            }

            var user = await otherGuild.GetUserAsync(Context.User.Id);
            if (user is null)
            {
                await Interaction.ReplyErrorAsync("You are not in the other guild");
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
                Name = name, //todo: Name limit, format
                GuildAId = Context.Guild.Id,
                ChannelAId = Context.Channel.Id,
                GuildBId = otherGuild.Id,
                ChannelBId = otherChannel.Id
            };
            _dbContext.Bridges.Add(dbBridge);
            _logger.LogDebug("{intTag} Adding bridge {bridge} to channel {channel} in guild {guild}", GetIntTag(), dbBridge, Context.Channel.Log(), Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Added bridge {bridge} to channel {channel} in guild {guild}", GetIntTag(), dbBridge, Context.Channel.Log(), Context.Guild.Log());
            await ((IMessageChannel)otherChannel).SendMessageAsync(embed: EmbedFactory.Default($"{user.Mention} created bridge to channel {Context.Channel.Id} in guild {Context.Guild.Id} with id {dbBridge.BridgeId}"));
            await Interaction.ReplyAsync($"Created bridge to channel {otherChannel.Id} in guild {otherGuild.Id} with id {dbBridge.BridgeId}");
        }

        [SlashCommand("remove", "Remove bridges from channel")]
        public async Task RemoveBridgeAsync //todo: [TESTING] Does removal work?
        (
            [Summary(description: "[Optional] Bridge Id")] ulong bridgeId = 0
        )
        {
            var query = bridgeId == 0
                ? _dbContext.Bridges.ForChannel(Context.Channel.Id)
                : _dbContext.Bridges.Where(x => x.BridgeId == bridgeId);

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
            await Interaction.ReplyAsync($"Removed {bridges.Count} bridges");
        }
    }
}
