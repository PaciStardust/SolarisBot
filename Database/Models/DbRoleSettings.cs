using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(RoleId))]
    public class DbRoleSettings
    {
        public ulong RoleId { get; set; } = ulong.MinValue;
        [ForeignKey(nameof(DbRoleGroup))]
        public ulong RoleGroupId { get; set; } = ulong.MinValue;
        public string Identifier { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public virtual DbRoleGroup RoleGroup { get; set; } = null!;

        public DbRoleSettings() { }

        public override string ToString()
            => $"{Identifier}(Group {RoleGroupId})";
    }
}
