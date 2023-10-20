using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(GuildId))]
    public class DbGuildConfig
    {
        public ulong GuildId { get; set; } = ulong.MinValue;
        public ulong VouchRoleId { get; set; } = ulong.MinValue;
        public ulong VouchPermissionRoleId { get; set; } = ulong.MinValue;
        public ulong CustomColorPermissionRoleId { get; set; } = ulong.MinValue;
        public bool JokeRenameOn { get; set; } = false;
        public ulong JokeRenameTimeoutMin { get; set; } = ulong.MinValue;
        public ulong JokeRenameTimeoutMax { get; set; } = ulong.MinValue;
        public ulong MagicRoleId { get; set; } = ulong.MinValue;
        public ulong MagicRoleTimeout { get; set; } = ulong.MinValue;
        public ulong MagicRoleNextUse { get; set; } = ulong.MinValue;
        public bool MagicRoleRenameOn { get; set; } = false;
        public bool RemindersOn { get; set; } = false;
        public bool QuotesOn { get; set; } = false;
        public ulong AutoRoleId { get; set; } = ulong.MinValue;
        public ulong SpellcheckRoleId { get; set; } = ulong.MinValue;

        [ForeignKey(nameof(DbRoleGroup.GuildId))]
        public virtual ICollection<DbRoleGroup> RoleGroups { get; set; } = new HashSet<DbRoleGroup>();
        [ForeignKey(nameof(DbQuote.GuildId))]
        public virtual ICollection<DbQuote> Quotes { get; set; } = new HashSet<DbQuote>();
        [ForeignKey(nameof(DbJokeTimeout.GuildId))]
        public virtual ICollection<DbJokeTimeout> JokeTimeouts { get; set; } = new HashSet<DbJokeTimeout>();
        [ForeignKey(nameof(DbReminder.GuildId))]
        public virtual ICollection<DbReminder> Reminders { get; set; } = new HashSet<DbReminder>();

        public DbGuildConfig() { } //To avoid defaults not setting
    }
}
