using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database.Models
{
    [PrimaryKey(nameof(InteractionRecordId))]
    public class DbInteractionRecord : DbModelBase
    {
        public ulong InteractionRecordId { get; set; } = ulong.MinValue;
        public ulong InteractionCreatedAt { get; set; } = ulong.MinValue;
        public ulong InteractionCompletedAt { get; set; } = ulong.MinValue;
        public ulong GuildId { get; set; } = ulong.MinValue;
        public ulong ChannelId { get; set; } = ulong.MinValue;
        public ulong UserId { get; set; } = ulong.MinValue;
        public ulong InteractionId { get; set; } = ulong.MinValue;
        public bool Success { get; set; } = false;
        public string ModuleName { get; set; } = string.Empty;
        public string CommandName { get; set; } = string.Empty;
        public string MethodName { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string ErrorType { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string ErrorTrace { get; set; } = string.Empty;
    }
}
