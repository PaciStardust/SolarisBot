using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(RId))]
    public class DbRole
    {
        public ulong RId { get; set; } = 0;
        public ulong RgId { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public DbRole() { }
    }
}
