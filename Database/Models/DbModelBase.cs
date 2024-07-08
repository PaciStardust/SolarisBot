namespace SolarisBot.Database
{
    public abstract class DbModelBase
    {
        public DbModelBase()
        {
            CreatedAt = Utils.GetCurrentUnix();
            UpdatedAt = CreatedAt;
        }

        public ulong CreatedAt { get; set; }
        public ulong UpdatedAt { get; set; }
    }
}
