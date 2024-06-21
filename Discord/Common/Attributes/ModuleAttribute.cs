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

        /// <summary>
        /// Checks against a list of disabled modules if the module should be disabled
        /// </summary>
        /// <param name="disabledList">List of disabled modules</param>
        /// <returns>Disabled?</returns>
        internal bool IsDisabled(IEnumerable<string> disabledList)
            => disabledList.Any(x => ModuleNames.Any(y => y.StartsWith(x, StringComparison.OrdinalIgnoreCase)));
    }
}
