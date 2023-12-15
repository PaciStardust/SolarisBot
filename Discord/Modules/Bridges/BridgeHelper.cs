using Discord.WebSocket;
using Discord;
using SolarisBot.Database.Models;
using SolarisBot.Discord.Common;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Discord.Modules.Bridges
{
    internal static class BridgeHelper
    {
        internal static async Task TryNotifyChannelForBridgeDeletionAsync(IMessageChannel msgChannel, IChannel? otherChannel, DbBridge bridge, ILogger logger, bool bridgeGroupB = false)
        {
            SocketGuildChannel? gOtherChannel = null;
            if (otherChannel is not null && otherChannel is SocketGuildChannel goc)
                gOtherChannel = goc;

            try
            {
                logger.LogDebug("Notifying channel {channel} in guild {guild} of deleted bridge {bridge}", otherChannel?.Log() ?? (bridgeGroupB ? bridge.ChannelBId : bridge.ChannelAId).ToString(), gOtherChannel?.Guild.Log() ?? (bridgeGroupB ? bridge.GuildBId : bridge.GuildAId).ToString(), bridge);
                var notifyEmbed = EmbedFactory.Default($"Bridge {bridge.ToDiscordInfoString()} to channel {otherChannel?.ToDiscordInfoString() ?? (bridgeGroupB ? bridge.ChannelBId : bridge.ChannelAId).ToString()} in guild {gOtherChannel?.Guild.ToDiscordInfoString() ?? $"**{(bridgeGroupB ? bridge.GuildBId : bridge.GuildAId)}**"} has been removed");
                await msgChannel.SendMessageAsync(embed: notifyEmbed);
                logger.LogInformation("Notified channel {channel} in guild {guild} of deleted bridge {bridge}", otherChannel?.Log() ?? (bridgeGroupB ? bridge.ChannelBId : bridge.ChannelAId).ToString(), gOtherChannel?.Guild.Log() ?? (bridgeGroupB ? bridge.GuildBId : bridge.GuildAId).ToString(), bridge);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed notifying channel {channel} in guild {guild} of deleted bridge {bridge}", otherChannel?.Log() ?? (bridgeGroupB ? bridge.ChannelBId : bridge.ChannelAId).ToString(), gOtherChannel?.Guild.Log() ?? (bridgeGroupB ? bridge.GuildBId : bridge.GuildAId).ToString(), bridge);
            }
        }
    }
}
