using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Database.Models
{
    [PrimaryKey(nameof(VouchActionId))]
    public class DbVouchAction : DbModelBase
    {
        public DbVouchAction()
        {
            VouchedAt = Utils.GetCurrentUnix(); //Yes this is doubled from "CreatedAt" but this is intentional to not mess with manual insertion
        }

        public ulong VouchActionId { get; set; } = ulong.MinValue;
        public ulong GuildId { get; set; } = ulong.MinValue;
        public ulong ExecutingUserId { get; set; } = ulong.MinValue;
        public ulong TargetUserId { get; set; } = ulong.MinValue;
        public ulong VouchedAt {  get; set; }
    }
}
