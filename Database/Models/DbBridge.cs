using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database.Models
{
    [PrimaryKey(nameof(BridgeId))]
    public class DbBridge
    {
        public ulong BridgeId { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public ulong GuildAId { get; set; } = 0;
        public ulong ChannelAId { get; set; } = 0;
        public ulong GuildBId { get; set; } = 0;
        public ulong ChannelBId { get; set; } = 0;

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
