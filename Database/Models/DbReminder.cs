using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database.Models
{
    [PrimaryKey(nameof(RId))]
    public class DbReminder
    {
        public ulong RId { get; set; } = 0;
        public ulong GId { get; set; } = 0;
        public ulong UserId { get; set; } = 0;
        public bool IsPrivate { get; set; } = false;
        public ulong ChannelId { get; set; } = 0;
        public ulong Time { get; set; } = 0;
        public string Text { get; set; } = string.Empty;
    }
}
