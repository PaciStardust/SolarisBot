namespace SolarisBot.Discord.Common
{
    public struct DeletedRole<T>(T value)
    {
        public T Value { get; } = value;
    }
}
