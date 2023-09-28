using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Database.Models
{
    [PrimaryKey(nameof(BdayAnnouncementId))]
    public class DbBdayAnnouncement
    {
        public ulong BdayAnnouncementId { get; set; } = 0;
        public ulong UserId { get; set; } = 0;
        public ulong GuildId { get; set; } = 0;

        public DbBdayAnnouncement() { }
    }
}
