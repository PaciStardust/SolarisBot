using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(ReminderId))]
    public class DbReminder
    {
        public ulong ReminderId { get; set; } = 0;
        public ulong GuildId { get; set; } = 0;
        public ulong UserId { get; set; } = 0;
        public ulong ChannelId { get; set; } = 0;
        public ulong Time { get; set; } = 0;
        public ulong Created { get; set; } = 0;
        public string Text { get; set; } = string.Empty;

        public DbReminder() { }

        public override string ToString()
            => $"{Text}(Unix {Created}>{Time})";
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
