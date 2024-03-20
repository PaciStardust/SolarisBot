using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(JokeTimeoutId))]
    public class DbJokeTimeout
    {
        public ulong JokeTimeoutId { get; set; } = 0;
        public ulong UserId { get; set; } = 0;
        public ulong GuildId { get; set; } = 0;
        public ulong NextUse { get; set; } = 0;

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
