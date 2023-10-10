using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(JokeTimeoutId))]
    public class DbJokeTimeout
    {
        public ulong JokeTimeoutId { get; set; } = ulong.MinValue;
        public ulong UserId { get; set; } = ulong.MinValue;
        [ForeignKey(nameof(DbGuildSettings))]
        public ulong GuildId { get; set; } = ulong.MinValue;
        public ulong NextUse { get; set; } = ulong.MinValue;

        public DbJokeTimeout() { }
    }
}
