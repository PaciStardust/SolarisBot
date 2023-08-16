using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(RgId))]
    public class DbRoleGroup
    {
        public ulong RgId { get; set; } = 0;
        public ulong GId { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public bool AllowOnlyOne { get; set; } = false;

        [ForeignKey(nameof(DbRole.RgId))]
        public virtual ICollection<DbRole> Roles { get; set; } = new HashSet<DbRole>();

        public DbRoleGroup() { } //To avoid defaults not setting
    }
}
