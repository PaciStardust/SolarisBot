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
        public ulong ChannelId { get; set; } = ulong.MinValue;
        public ulong GuildId { get; set; } = ulong.MinValue;
        public string Regex { get; set; } = string.Empty;
        public ulong AppliedRoleId { get; set; } = ulong.MinValue;
        public string PunishmentMessage { get; set; } = string.Empty;
        public bool PunishmentDelete { get; set; } = false;
        public bool IsDeleted { get; set; } = false; //todo: impl
        public ulong CreatedAt { get; set; } = ulong.MinValue; //todo: impl
        public ulong DeletedAt { get; set; } = ulong.MinValue; //todo: impl

        public override string ToString()
            => Regex;
    }
}
