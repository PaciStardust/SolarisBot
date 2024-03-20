using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Database.Models
{
    [PrimaryKey(nameof(ChannelId))]
    public class DbRegexChannel
    {
        public ulong ChannelId { get; set; } = 0;
        public ulong GuildId { get; set; } = 0;
        public string Regex { get; set; } = string.Empty;
        public ulong AppliedRoleId { get; set; } = 0;
        public string PunishmentMessage { get; set; } = string.Empty;
        public bool PunishmentDelete { get; set; } = false;

        public override string ToString()
            => Regex;
    }
}
