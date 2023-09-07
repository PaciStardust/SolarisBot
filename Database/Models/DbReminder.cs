using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database
{
    [PrimaryKey(nameof(RId))]
    public class DbReminder
    {
        public ulong RId { get; set; } = 0;
        public ulong GId { get; set; } = 0;
        public ulong UserId { get; set; } = 0;
        public ulong ChannelId { get; set; } = 0;
        public ulong Time { get; set; } = 0;
        public ulong Created { get; set; } = 0;
        public string Text { get; set; } = string.Empty;

        public override string ToString()
            => $"{Text}(Unix {Created}>{Time})";
    }
}
