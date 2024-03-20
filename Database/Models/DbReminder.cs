using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(ReminderId))]
    public class DbReminder : DbModelBase //todo: impl create + update
    {
        public ulong ReminderId { get; set; } = ulong.MinValue;
        public ulong GuildId { get; set; } = ulong.MinValue;
        public ulong UserId { get; set; } = ulong.MinValue;
        public ulong ChannelId { get; set; } = ulong.MinValue;
        public ulong RemindAt { get; set; } = ulong.MinValue;
        public string Text { get; set; } = string.Empty;
        public bool IsDeleted { get; set; } = false; //todo: impl

        public DbReminder() { }

        public override string ToString()
            => $"{Text}(Unix {CreatedAt}>{RemindAt})";
    }

    internal static class DbReminderExtensions
    {
        internal static IQueryable<DbReminder> ForGuild(this IQueryable<DbReminder> query, ulong id)
            => query.Where(x => x.GuildId == id);

        internal static IQueryable<DbReminder> ForUser(this IQueryable<DbReminder> query, ulong id)
            => query.Where(x => x.UserId == id);

        internal static IQueryable<DbReminder> ForChannel(this IQueryable<DbReminder> query, ulong id)
            => query.Where(x => x.ChannelId == id);
    }
}
