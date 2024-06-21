namespace SolarisBot.Discord.Common
{
    internal static class StandardError
    {
        internal const string NoResults = "Request yielded no results";

        internal static string InvalidParameter(string parameterName)
            => $"Value for parameter {parameterName} is invalid";

        internal static string FailedConversion(string from, string to)
            => $"Unable to convert {from} to {to}";

        internal static string DeletedRole(string roleName)
            => $"{roleName} role could not be found in guild, it might have been deleted";

        internal static string DisabledFeature(string featureName)
            => $"{featureName} is not enabled in this guild";
    }
}
