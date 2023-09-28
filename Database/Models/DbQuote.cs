using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(QuoteId))]
    public class DbQuote
    {
        public ulong QuoteId { get; set; } = 0;
        public ulong GuildId { get; set; } = 0;
        public string Text { get; set; } = string.Empty;
        public ulong AuthorId { get; set; } = 0;
        public ulong Time { get; set; } = 0;
        public ulong CreatorId { get; set; } = 0;
        public ulong ChannelId { get; set; } = 0;
        public ulong MessageId { get; set; } = 0;

        public DbQuote() { }

        public override string ToString()
        {
            var len = Text.Length;
            var text = len > 30 ? Text[..27] + "..." : Text;
            return $"{QuoteId}: \"{text}\" - <@{AuthorId}>";
        }
    }
}
