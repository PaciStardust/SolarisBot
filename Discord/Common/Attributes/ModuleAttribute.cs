namespace SolarisBot.Discord.Common.Attributes
{
    /// <summary>
    /// Attribute for automatically loading modules
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ModuleAttribute : Attribute
    {
        internal string[] ModuleNames { get; }
        internal ModuleAttribute(params string[] moduleNames)
        {
            ModuleNames = moduleNames;
        }

        internal bool IsDisabled(IEnumerable<string> disabledList)
            => disabledList.Any(x => ModuleNames.Any(y => y.StartsWith(x, StringComparison.OrdinalIgnoreCase)));
    }
}
