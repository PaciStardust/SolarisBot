using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(QuoteId))]
    public class DbQuote
    {
        public ulong QuoteId { get; set; } = ulong.MinValue;
        public ulong GuildId { get; set; } = ulong.MinValue;
        public string Text { get; set; } = string.Empty;
        public ulong AuthorId { get; set; } = ulong.MinValue;
        public ulong Time { get; set; } = ulong.MinValue;
        public ulong CreatorId { get; set; } = ulong.MinValue;
        public ulong ChannelId { get; set; } = ulong.MinValue;
        public ulong MessageId { get; set; } = ulong.MinValue;

        public DbQuote() { }

        public override string ToString()
        {
            var len = Text.Length;
            var text = len > 30 ? Text[..27] + "..." : Text;
            return $"{QuoteId}: \"{text}\" - <@{AuthorId}>";
        }
    }
}
