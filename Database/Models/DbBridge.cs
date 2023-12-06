using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database.Models
{
    [PrimaryKey(nameof(BridgeId))]
    internal class DbBridge
    {
        public ulong BridgeId { get; set; } = ulong.MinValue;
        public ulong ChannelAId { get; set; } = ulong.MinValue;
        public ulong ChannelBId { get; set; } = ulong.MinValue;

        public DbBridge() { }

        public override string ToString()
            => $"{BridgeId}: {ChannelAId} <-> {ChannelBId}";
    }

    internal static class DbBridgeExtensions
    {
        internal static IQueryable<DbBridge> ForChannel(this IQueryable<DbBridge> query, ulong id)
            => query.Where(x => x.ChannelBId == id || x.ChannelAId == id);
    }
}
