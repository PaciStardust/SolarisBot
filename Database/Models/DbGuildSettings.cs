using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(GuildId))]
    public class DbGuildSettings
    {
        public ulong GuildId { get; set; } = 0;
        public ulong VouchRoleId { get; set; } = 0;
        public ulong VouchPermissionRoleId { get; set; } = 0;
        public ulong CustomColorPermissionRoleId { get; set; } = 0;
        public bool JokeRenameOn { get; set; } = false;
        public ulong JokeRenameTimeoutMin { get; set; } = 0;
        public ulong JokeRenameTimeoutMax { get; set; } = 0;
        public ulong MagicRoleId { get; set; } = 0;
        public ulong MagicRoleTimeout { get; set; } = 0;
        public ulong MagicRoleNextUse { get; set; } = 0;
        public bool MagicRoleRenameOn { get; set; } = false;
        public bool RemindersOn { get; set; } = false;
        public bool QuotesOn { get; set; } = false;
        public ulong AutoRoleId { get; set; } = 0;
        public ulong BirthdayChannelId { get; set; } = 0;
        public ulong BirthdayRoleId { get; set; } = 0;

        [ForeignKey(nameof(DbRoleGroup.GuildId))]
        public virtual ICollection<DbRoleGroup> RoleGroups { get; set; } = new HashSet<DbRoleGroup>();

        [ForeignKey(nameof(DbQuote.GuildId))]
        public virtual ICollection<DbQuote> Quotes { get; set; } = new HashSet<DbQuote>();

        [ForeignKey(nameof(DbJokeTimeout.GuildId))]
        public virtual ICollection<DbJokeTimeout> JokeTimeouts { get; set; } = new HashSet<DbJokeTimeout>();

        [ForeignKey(nameof(DbJokeTimeout.GuildId))]
        public virtual ICollection<DbReminder> Reminders { get; set; } = new HashSet<DbReminder>();

        public DbGuildSettings() { } //To avoid defaults not setting
    }
}
