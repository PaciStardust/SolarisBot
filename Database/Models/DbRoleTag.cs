using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(TId))]
    public class DbRoleTag
    {
        public ulong TId { get; set; } = 0;
        public ulong GId { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public bool AllowOnlyOne { get; set; } = false;

        [ForeignKey(nameof(DbRole.TId))]
        public virtual ICollection<DbRole> Roles { get; set; } = new HashSet<DbRole>();

        public DbRoleTag() { } //To avoid defaults not setting
    }
}
