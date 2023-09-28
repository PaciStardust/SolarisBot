using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarisBot.Database.Models
{
    [PrimaryKey(nameof(UserId))]
    public class DbUserSettings
    {
        public ulong UserId { get; set; } = 0;
        public ulong Birthday { get; set; } = 0;

        public DbGuildSettings GuildSettings { get; set; } = null!;

        [ForeignKey(nameof(DbBdayAnnouncement.GuildId))]
        public virtual ICollection<DbBdayAnnouncement> BdayAnnouncements { get; set; } = new HashSet<DbBdayAnnouncement>();

        public DbUserSettings() { }
    }
}
