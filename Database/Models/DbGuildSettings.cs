using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(GuildId))]
    public class DbGuildSettings
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

        public virtual ICollection<DbRoleGroup> RoleGroups { get; set; } = new HashSet<DbRoleGroup>();
        public virtual ICollection<DbQuote> Quotes { get; set; } = new HashSet<DbQuote>();
        public virtual ICollection<DbJokeTimeout> JokeTimeouts { get; set; } = new HashSet<DbJokeTimeout>();
        public virtual ICollection<DbReminder> Reminders { get; set; } = new HashSet<DbReminder>();

        public DbGuildSettings() { } //To avoid defaults not setting
    }
}
