using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(RId))]
    public class DbRole
    {
        public ulong RId { get; set; } = 0;
        public ulong TId { get; set; } = 0;
        public string Name { get; set; } = string.Empty;

        public DbRole() { }
    }
}
