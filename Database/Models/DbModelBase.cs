using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Database
{
    public abstract class DbModelBase
    {
        public ulong CreatedAt { get; set; } = ulong.MinValue;
        public ulong UpdatedAt { get; set; } = ulong.MinValue;
    }
}
