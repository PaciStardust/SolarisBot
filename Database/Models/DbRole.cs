﻿using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(RId))]
    public class DbRole
    {
        public ulong RId { get; set; } = 0;
        public ulong RgId { get; set; } = 0;
        public string Identifier { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public virtual DbRoleGroup RoleGroup { get; set; } = null!;

        public DbRole() { }
    }
}
