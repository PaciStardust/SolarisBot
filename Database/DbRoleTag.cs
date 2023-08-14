using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(TId))]
    internal sealed class DbRoleTag
    {
        public ulong TId { get; set; } = 0;
        public ulong GId { get; set; } = 0;
        public string Name { get; set; } = string.Empty;
        public bool AllowOnlyOne { get; set; } = false;

        [ForeignKey(nameof(DbRole.TId))]
        public ICollection<DbRole> Roles { get; set; } = new HashSet<DbRole>();

        public DbRoleTag() { } //To avoid defaults not setting
    }
}
