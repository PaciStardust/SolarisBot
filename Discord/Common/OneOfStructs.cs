namespace SolarisBot.Discord.Common
{
    public struct DeletedRole<T>(T value) //todo: [REFACTOR] Is this needed?
    {
        public T Value { get; } = value;
    }
}
