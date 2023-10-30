namespace SolarisBot.Discord.Common.Attributes
{
    /// <summary>
    /// Attribute for automatically loading modules
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    internal sealed class ModuleAttribute : Attribute
    {
        internal string ModuleName { get; }
        internal ModuleAttribute(string moduleName)
        {
            ModuleName = moduleName.ToLower();
        }

        internal bool IsDisabled(IEnumerable<string> disabledList)
            => disabledList.Any(x => ModuleName.StartsWith(x.ToLower()));
    }
}
