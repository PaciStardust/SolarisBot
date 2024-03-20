using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(RoleId))]
    public class DbRoleConfig
    {
        public ulong RoleId { get; set; } = ulong.MinValue;
        public ulong RoleGroupId { get; set; } = ulong.MinValue;
        public string Identifier { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false; //todo: impl
        public ulong CreatedAt { get; set; } = ulong.MinValue; //todo: impl
        public ulong UpdatedAt { get; set; } = ulong.MinValue; //todo: impl

        public virtual DbRoleGroup RoleGroup { get; set; } = null!;

        public DbRoleConfig() { }

        public override string ToString()
            => $"{Identifier}(Group {RoleGroupId})";
    }
}
