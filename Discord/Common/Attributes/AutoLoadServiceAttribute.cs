namespace SolarisBot.Discord.Common.Attributes
{
    /// <summary>
    /// Flag for enabling automatic loading of services, lifetime attributes do not affect hosted services
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class AutoLoadServiceAttribute : Attribute
    {
        internal Lifetime Lifetime { get; }
        internal AutoLoadServiceAttribute(Lifetime lifetime = Lifetime.Singleton)
        {
            Lifetime = lifetime;
        }
    }

    internal enum Lifetime
    {
        Singleton,
        Scoped,
        Transient
    }
}
