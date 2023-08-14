using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(JtId))]
    internal sealed class DbJokeTimeout
    {
        public ulong JtId { get; set; } = 0;
        public ulong UserId { get; set; } = 0;
        public ulong GId { get; set; } = 0;
        public ulong NextUse { get; set; } = 0;

        public DbJokeTimeout() { }
    }
}
