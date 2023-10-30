namespace SolarisBot.Discord.Common.Attributes
{
    /// <summary>
    /// Flag for local-only interactions
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class LocalOnlyAttribute : Attribute
    {
    }
}
