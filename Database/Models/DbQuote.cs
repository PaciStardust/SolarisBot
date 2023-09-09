using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(QId))]
    public class DbQuote //todo: implement quotes
    {
        public ulong QId { get; set; } = 0;
        public ulong GId { get; set; } = 0;
        public string Text { get; set; } = string.Empty;
        public ulong AuthorId { get; set; } = 0;
        public ulong Time { get; set; } = 0;
        public ulong CreatorId { get; set; } = 0;
        public ulong ChannelId { get; set; } = 0;
        public ulong MessageId { get; set; } = 0;

        public DbQuote() { }
    }
}
