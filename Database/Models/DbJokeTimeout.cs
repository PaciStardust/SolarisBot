using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(JokeTimeoutId))]
    public class DbJokeTimeout : DbModelBase
    {
        public ulong JokeTimeoutId { get; set; } = ulong.MinValue;
        public ulong UserId { get; set; } = ulong.MinValue;
        public ulong GuildId { get; set; } = ulong.MinValue;
        public ulong NextUse { get; set; } = ulong.MinValue;

        public DbJokeTimeout() { }
    }

    internal static class DbJokeTimeoutExtensions
    {
        internal static IQueryable<DbJokeTimeout> ForGuild(this IQueryable<DbJokeTimeout> query, ulong id)
            => query.Where(x => x.GuildId == id);

        internal static IQueryable<DbJokeTimeout> ForUser(this IQueryable<DbJokeTimeout> query, ulong id)
            => query.Where(x => x.UserId == id);
    }
}
