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

        internal static string RoleRequired(ulong roleId)
            => $"You do not have the required role <@&{roleId}>";

        internal static string InvalidIdentifier(string identifier)
            => $"Identifier {identifier} is invalid, identifiers can only contain letters, numbers, and spaces and must be between 2 and 20 characters long";
    }
}
