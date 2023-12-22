using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database.Models
{
    [PrimaryKey(nameof(BridgeId))]
    internal class DbBridge
    {
        public ulong BridgeId { get; set; } = ulong.MinValue;
        public string Name { get; set; } = string.Empty;
        public ulong GuildAId { get; set; } = ulong.MinValue;
        public ulong ChannelAId { get; set; } = ulong.MinValue;
        public ulong GuildBId { get; set; } = ulong.MinValue;
        public ulong ChannelBId { get; set; } = ulong.MinValue;

        public DbBridge() { }

        public override string ToString()
            => $"{BridgeId}: {Name} {ChannelAId}({GuildAId}) <=> {ChannelBId}({GuildBId})";
        internal string ToDiscordInfoString()
            => $"**{Name}** *({BridgeId})*";
    }

    internal static class DbBridgeExtensions
    {
        internal static IQueryable<DbBridge> ForChannel(this IQueryable<DbBridge> query, ulong id)
            => query.Where(x => x.ChannelBId == id || x.ChannelAId == id);

        internal static IQueryable<DbBridge> ForGuild(this IQueryable<DbBridge> query, ulong id)
            => query.Where(x => x.GuildBId == id || x.GuildAId == id);
    }
}
