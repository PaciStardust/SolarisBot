using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(GuildId))]
    public class DbGuildConfig : DbModelBase
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
        public bool StealNicknameOn { get; set; } = false;
        public bool GififyOn { get; set; } = false;
        public ulong QuarantineRoleId { get; set; } = ulong.MinValue;
        public ulong UserAnalysisChannelId { get; set; } = ulong.MinValue;
        public int UserAnalysisWarnAt { get; set; } = int.MaxValue;
        public int UserAnalysisKickAt { get; set; } = int.MaxValue;
        public int UserAnalysisBanAt { get; set; } = int.MaxValue;

        [ForeignKey(nameof(DbRoleGroup.GuildId))]
        public virtual ICollection<DbRoleGroup> RoleGroups { get; set; } = new HashSet<DbRoleGroup>();
        [ForeignKey(nameof(DbQuote.GuildId))]
        public virtual ICollection<DbQuote> Quotes { get; set; } = new HashSet<DbQuote>();
        [ForeignKey(nameof(DbJokeTimeout.GuildId))]
        public virtual ICollection<DbJokeTimeout> JokeTimeouts { get; set; } = new HashSet<DbJokeTimeout>();
        [ForeignKey(nameof(DbReminder.GuildId))]
        public virtual ICollection<DbReminder> Reminders { get; set; } = new HashSet<DbReminder>();
        [ForeignKey(nameof(DbRegexChannel.GuildId))]
        public virtual ICollection<DbRegexChannel> RegexChannels { get; set; } = new HashSet<DbRegexChannel>();

        public DbGuildConfig() { } //To avoid defaults not setting

        public bool VouchingOn => VouchRoleId != ulong.MinValue && VouchPermissionRoleId != ulong.MinValue;
    }

    internal static class DbGuildConfigExtensions
    {
        /// <summary>
        /// Get an untracked guild by Id
        /// </summary>
        /// <returns>Guild matching ID or null, if no match is found or an error occured</returns>
        internal static async Task<DbGuildConfig?> GetGuildByIdAsync(this DatabaseContext ctx, ulong id, Func<DbSet<DbGuildConfig>, IQueryable<DbGuildConfig>>? include = null)
        {
            if (include is null)
                return await ctx.GuildConfigs.FirstOrDefaultAsync(x => x.GuildId == id);
            return await include(ctx.GuildConfigs).FirstOrDefaultAsync(x => x.GuildId == id);
        }

        /// <summary>
        /// Get a tracked guild by ID
        /// </summary>
        /// <returns>Tracked guild matching id, or a new instance, automatically added to database, or null on error</returns>
        internal static async Task<DbGuildConfig> GetOrCreateTrackedGuildAsync(this DatabaseContext ctx, ulong id, Func<DbSet<DbGuildConfig>, IQueryable<DbGuildConfig>>? include = null)
        {
            DbGuildConfig? cfg;
            if (include is null)
                cfg = await ctx.GuildConfigs.AsTracking().FirstOrDefaultAsync(x => x.GuildId == id);
            else
                cfg = await include(ctx.GuildConfigs).AsTracking().FirstOrDefaultAsync(x => x.GuildId == id);

            if (cfg is null)
            {
                cfg = new() { GuildId = id };
                ctx.GuildConfigs.Add(cfg);
            }
            return cfg;
        }
    }
}
