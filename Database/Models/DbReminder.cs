using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(ReminderId))]
    public class DbReminder
    {
        public ulong ReminderId { get; set; } = ulong.MinValue;
        public ulong GuildId { get; set; } = ulong.MinValue;
        public ulong UserId { get; set; } = ulong.MinValue;
        public ulong ChannelId { get; set; } = ulong.MinValue;
        public ulong Time { get; set; } = ulong.MinValue;
        public ulong Created { get; set; } = ulong.MinValue;
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
    }
}
