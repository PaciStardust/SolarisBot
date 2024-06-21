

namespace SolarisBot.Discord.Common
{
    internal static class StandardError
    {
        internal const string NoResults = "Request yielded no results";

        internal static string InvalidParameter(string parameterName)
            => $"Value for {parameterName} parameter is invalid";

        internal static string FailedConversion(string from, string to)
            => $"Unable to convert {from} to {to}";
    }
}
