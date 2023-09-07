using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(JtId))]
    public class DbJokeTimeout
    {
        public ulong JtId { get; set; } = 0;
        public ulong UserId { get; set; } = 0;
        public ulong GId { get; set; } = 0;
        public ulong NextUse { get; set; } = 0;

        public DbJokeTimeout() { }
    }
}
