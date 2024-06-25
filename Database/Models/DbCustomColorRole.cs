namespace SolarisBot.Database.Models
{
    public class DbCustomColorRole : DbModelBase
    {
        public ulong CustomColorRoleId { get; set; } = ulong.MinValue;
        public ulong GuildId { get; set; } = ulong.MinValue;
        public ulong RoleId { get; set; } = ulong.MinValue;
        public ulong UserId { get; set; } = ulong.MinValue;
    }
    internal static class DbCustomColorRoleExtensions
    {
        internal static IQueryable<DbCustomColorRole> ForGuild(this IQueryable<DbCustomColorRole> query, ulong id)
            => query.Where(x => x.GuildId == id);

        internal static IQueryable<DbCustomColorRole> ForUser(this IQueryable<DbCustomColorRole> query, ulong id)
            => query.Where(x => x.UserId == id);
    }
}
