using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(QId))]
    internal sealed class DbQuote
    {
        public ulong QId { get; set; } = 0;
        public ulong GId { get; set; } = 0;
        public string Text { get; set; } = string.Empty;
        public ulong AuthorId { get; set; } = 0;
        public ulong Time { get; set; } = 0;
        public ulong CreatorId { get; set; } = 0;

        public DbQuote() { }
    }
}
