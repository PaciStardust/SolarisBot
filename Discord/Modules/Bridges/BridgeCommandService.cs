using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Bridges
{
    [Module("bridges"), AutoLoadService]
    internal class BridgeCommandService
    {
        private readonly DatabaseService _dbService;
        private readonly BotConfig _botConfig;
        private readonly DiscordSocketClient _discordClient;
        private readonly ILogger<BridgeCommandService> _logger;

        public BridgeCommandService(DatabaseService dbService, BotConfig botConfig, DiscordSocketClient discordClient, ILogger<BridgeCommandService> logger)
        {
            _dbService = dbService;
            _botConfig = botConfig;
            _discordClient = discordClient;
            _logger = logger;
        }

        /// <summary>
        /// Returns all bridges connected to a channel or guild
        /// </summary>
        /// <param name="channelOnly">Limits search to only a channel</param>
        /// <param name="id">Id of guild or channel</param>
        /// <returns>A list of all bridges</returns>
        internal async Task<DbBridge[]> GetConnectedBridgesAsync(bool channelOnly, ulong id)
        {
            using var dbCtx = _dbService.GetContext();

            var query = channelOnly
                ? dbCtx.Bridges.ForChannel(id)
                : dbCtx.Bridges.ForGuild(id);

            return await query.ToArrayAsync();
        }

        /// <summary>
        /// Creates a bridge between 2 channels. Bot and User require "Manage Channels" in both channels
        /// </summary>
        /// <param name="bridgeName">Name of bridge</param>
        /// <param name="targetGuildId">Id of target guild</param>
        /// <param name="targetChannelId">Id of target channel</param>
        /// <param name="executingChannelId">Id of executing channel</param>
        /// <param name="executingGuildId">Id of executing guild</param>
        /// <param name="executingUserId">Id of executing user</param>
        /// <returns>DbGuild on success, Reason on fail</returns>
        internal async Task<OneOf<Success<DbBridge>, Error<string>>> CreateBridgeAsync(string bridgeName, ulong targetGuildId, ulong targetChannelId, ulong executingChannelId, ulong executingGuildId, ulong executingUserId) //todo: dont use IDs?
        {
            if (executingChannelId == targetChannelId)
                return new Error<string>("A bridge can not be created to the same channel");

            var bridgeNameTrimmed = bridgeName.Trim();
            if (!DiscordUtils.IsIdentifierValid(bridgeNameTrimmed))
                return new Error<string>("Identifier is invalid"); //todo: shortcut

            using var dbCtx = _dbService.GetContext();

            //Checking if bridge limit is reached
            var bridgesExecutingGuild = await dbCtx.Bridges.ForGuild(executingGuildId).CountAsync();
            if (bridgesExecutingGuild > _botConfig.MaxBridgesPerGuild)
                return new Error<string>($"Executing guild already has the maximum amount of bridges *({_botConfig.MaxBridgesPerGuild})*");
            var bridgesTargetGuild = await dbCtx.Bridges.ForGuild(targetChannelId).CountAsync();
            if (bridgesTargetGuild > _botConfig.MaxBridgesPerGuild)
                return new Error<string>($"Target guild already has the maximum amount of bridges *({_botConfig.MaxBridgesPerGuild})*");

            //Checking for a duplicate
            var duplicate = await dbCtx.Bridges.ForGuild(targetGuildId).ForGuild(executingGuildId).FirstOrDefaultAsync();
            if (duplicate is not null)
                return new Error<string>($"This bridge already exists as **{duplicate.Name}**");

            //Guild & Channel availability - Executing
            var executingGuild = _discordClient.GetGuild(executingGuildId);
            if (executingGuild is null)
                return new Error<string>($"Executing guild with Id {executingGuildId} could not be found");
            var executingChannel = executingGuild.GetChannel(executingChannelId);
            if (executingChannel is null)
                return new Error<string>($"Executing channel with Id {executingChannelId} could not be found");

            //Permissions check - Executing
            if (!executingGuild.CurrentUser.GetPermissions(executingChannel).ManageChannel)
                return new Error<string>("Bot does not have \"Manage Channel\" permission in the executing channel");
            var executingUser = executingGuild.GetUser(executingUserId);
            if (!executingUser.GetPermissions(executingChannel).ManageChannel)
                return new Error<string>("Executing user does not have \"Manage Channel\" permission in the executing channel");

            //Guild & Channel availability - Target
            var targetGuild = targetGuildId == executingGuildId ? executingGuild : _discordClient.GetGuild(targetGuildId);
            if (targetGuild is null)
                return new Error<string>($"Target guild with Id {targetGuildId} could not be found");
            var targetChannel = targetGuild.GetChannel(targetChannelId);
            if (targetChannel is null)
                return new Error<string>($"Target channel with Id {targetChannelId} could not be found");

            //Permissions check - Target
            if (!targetGuild.CurrentUser.GetPermissions(targetChannel).ManageChannel)
                return new Error<string>("Bot does not have \"Manage Channel\" permission in the target channel");
            var targetExecutingUser = targetGuild.GetUser(executingUserId);
            if (targetExecutingUser is null)
                return new Error<string>("Executing user could not be found in target guild");
            if (!targetExecutingUser.GetPermissions(targetChannel).ManageChannel)
                return new Error<string>("Executing user does not have \"Manage Channel\" permission in the target channel");

            var dbBridge = new DbBridge()
            {
                Name = bridgeNameTrimmed,
                GuildAId = executingChannelId,
                ChannelAId = executingGuildId,
                GuildBId = targetGuild.Id,
                ChannelBId = targetChannel.Id
            };
            dbCtx.Bridges.Add(dbBridge);

            _logger.LogDebug("Adding bridge {bridge} between channel {channel} in guild {guild} and channel {otherChannel} in guild {otherGuild}", dbBridge, executingChannel.Log(), executingGuild.Log(), targetChannel.Log(), targetGuild.Log());
            await dbCtx.SaveChangesAsync();
            _logger.LogInformation("Added bridge {bridge} between channel {channel} in guild {guild} and channel {otherChannel} in guild {otherGuild}", dbBridge, executingChannel.Log(), executingGuild.Log(), targetChannel.Log(), targetGuild.Log());

            await NotifyChannelOfBridgeCreationAsync(dbBridge, targetChannel, executingChannel, executingUser);
            await NotifyChannelOfBridgeCreationAsync(dbBridge,executingChannel, targetChannel, executingUser);
            
            return new Success<DbBridge>(dbBridge);
        }

        /// <summary>
        /// Notifies a channel of bridge creation
        /// </summary>
        /// <param name="dbBridge">Created bridge</param>
        /// <param name="targetGuildId">Id of guild to notify</param>
        /// <param name="targetChannelId">Id of channel to notify</param>
        /// <param name="executingGuildId">Id of creating guild</param>
        /// <param name="executingChannelId">Id of creating channel</param>
        /// <param name="executingUserId">Id of creating user</param>
        /// <returns>Success / Error with string / Error with exception</returns>
        private async Task<OneOf<Success, Error<string>, Error<Exception>>> NotifyChannelOfBridgeCreationAsync(DbBridge dbBridge, IGuildChannel targetChannel, IGuildChannel executingChannel, IUser executingUser) //todo: use IDs?
        {
            if (targetChannel is not IMessageChannel targetMessageChannel)
                return new Error<string>($"Target channel with Id {targetChannel.Id} could not be converted to messageChannel");

            try
            {
                _logger.LogDebug("Notifying channel {channel} in guild {guild} of created bridge {bridge}", targetChannel.Log(), targetChannel.Log(), dbBridge);
                var notifyEmbed = EmbedFactory.Default($"{executingUser.Mention} created bridge {dbBridge.ToDiscordInfoString()} to channel {executingChannel.ToDiscordInfoString()} in guild {executingChannel.Guild.ToDiscordInfoString()}");
                await targetMessageChannel.SendMessageAsync(embed: notifyEmbed);
                _logger.LogInformation("Notified channel {channel} in guild {guild} of broken bridge {bridge}", targetChannel.Log(), targetChannel.Log(), dbBridge);
                return new Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed notifying channel {channel} in guild {guild} of broken bridge {bridge}", targetChannel.Log(), targetChannel.Guild.Log(), dbBridge);
                return new Error<Exception>(ex);
            }
        }

        /// <summary>
        /// Removes bridges of ID x or for the whole channel
        /// </summary>
        /// <param name="guildId">Id of target guild</param>
        /// <param name="channelId">Id of target channel</param>
        /// <param name="bridgeId">Specific Bridge to delete</param>
        /// <returns>Amount deleted on success, reason on failure</returns>
        internal async Task<OneOf<Success<int>, Error<string>>> RemoveBridgesAsync(ulong guildId, ulong channelId, ulong? bridgeId)
        {
            using var dbCtx = _dbService.GetContext();

            var query = dbCtx.Bridges.ForGuild(guildId);
            query = bridgeId is null
                ? query.ForChannel(channelId)
                : query.Where(x => x.BridgeId == bridgeId);

            var bridges = await query.ToArrayAsync();
            if (bridges.Length == 0)
                return new Success<int>(0); //todo: should this be a success?

            dbCtx.Bridges.RemoveRange(bridges);
            _logger.LogDebug("Removing {bridgeCount} bridges in guild {guild}", bridges.Length, guildId);
            await dbCtx.SaveChangesAsync();
            _logger.LogInformation("Removed {bridgeCount} bridges in guild {guild}", bridges.Length, guildId);

            foreach (var bridge in bridges)
            {
                var channelA = _discordClient.GetGuild(bridge.GuildAId)?.GetChannel(bridge.ChannelAId);
                var channelB = _discordClient.GetGuild(bridge.GuildBId)?.GetChannel(bridge.ChannelBId);
                if (channelA is not null)
                    await NotifyChannelOfBridgeDeletionAsync(bridge, channelA, channelB);
                if (channelB is not null)
                    await NotifyChannelOfBridgeDeletionAsync(bridge, channelB, channelA);
            }

            return new Success<int>(bridges.Length);
        }

        /// <summary>
        /// Notifies a channel that a bridge has been deleted
        /// </summary>
        /// <param name="dbBridge">Bridge that was deleted</param>
        /// <param name="targetChannel">Channel to notify</param>
        /// <param name="executingChannel">Other channel</param>
        /// <returns>Success / Error as string / Error as Exception</returns>
        internal async Task<OneOf<Success, Error<string>, Error<Exception>>> NotifyChannelOfBridgeDeletionAsync(DbBridge dbBridge, IGuildChannel targetChannel, IGuildChannel? executingChannel) 
        {
            if (targetChannel is not IMessageChannel msgChannel)
                return new Error<string>($"Channel with Id {targetChannel.Id} could not be converted to messageChannel");

            try
            {
                _logger.LogDebug("Notifying channel {channel} in guild {guild} of broken bridge {bridge}", targetChannel.Log(), targetChannel.Guild.Log(), dbBridge);
                var targetGroupA = dbBridge.ChannelAId == targetChannel.GuildId;
                var notifyEmbed = EmbedFactory.Default($"Bridge {dbBridge.ToDiscordInfoString()} to channel {executingChannel?.ToDiscordInfoString() ?? (targetGroupA ? dbBridge.ChannelBId : dbBridge.ChannelAId).ToString()} in guild {executingChannel?.Guild.ToDiscordInfoString() ?? $"**{(targetGroupA ? dbBridge.GuildBId : dbBridge.GuildAId)}**"} has been broken");
                await msgChannel.SendMessageAsync(embed: notifyEmbed);
                _logger.LogInformation("Notified channel {channel} in guild {guild} of broken bridge {bridge}", targetChannel.Log(), targetChannel.Guild.Log(), dbBridge);
                return new Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed notifying channel {channel} in guild {guild} of broken bridge {bridge}", targetChannel.Log(), targetChannel.Guild.Log(), dbBridge);
                return new Error<Exception>(ex);
            }
        }
    }
}
