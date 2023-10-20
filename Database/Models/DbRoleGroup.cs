﻿using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(RoleGroupId))]
    public class DbRoleGroup
    {
        public ulong RoleGroupId { get; set; } = ulong.MinValue;
        public ulong GuildId { get; set; } = ulong.MinValue;
        public string Identifier { get; set; } = string.Empty;
        public bool AllowOnlyOne { get; set; } = false;
        public string Description { get; set; } = string.Empty;
        public ulong RequiredRoleId { get; set; } = ulong.MinValue;

        [ForeignKey(nameof(DbRoleConfig.RoleGroupId))]
        public virtual ICollection<DbRoleConfig> RoleConfigs { get; set; } = new HashSet<DbRoleConfig>();

        public DbRoleGroup() { } //To avoid defaults not setting

        public override string ToString()
            => $"{Identifier}(Guild {GuildId})";
    }
}
