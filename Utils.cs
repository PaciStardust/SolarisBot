using Bogus;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace SolarisBot
{
    internal static class Utils
    {
        internal static string PathConfigFile { get; private set; }
        internal static string PathDictionaryFile { get; private set; }
        internal static string PathDatabaseFile { get; private set; }
        internal static string PathConfigDirectory { get; private set; }

        static Utils()
        {
            var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
            PathConfigDirectory = Path.Combine(assemblyDirectory, "cfg");
            PathConfigFile = Path.GetFullPath(Path.Combine(PathConfigDirectory, "config.json"));
            PathDatabaseFile = Path.GetFullPath(Path.Combine(PathConfigDirectory, "database.db"));
            PathDictionaryFile = Path.GetFullPath(Path.Combine(PathConfigDirectory, "dictionary.txt"));
        }

        /// <summary>
        /// Gets current Unix as ULong
        /// </summary>
        /// <returns>Current Uunix Timestamp (seconds)</returns>
        internal static ulong GetCurrentUnix(ILogger? logger = null)
            => DateTimeOffset.Now.ToUnixTimeSeconds().AsUlong(logger);

        /// <summary>
        /// Converts a long value to a ulong value
        /// </summary>
        /// <param name="value">Long to convert</param>
        /// <returns>ulong value or MinValue, if parsing fails</returns>
        internal static ulong AsUlong(this long value, ILogger? logger = null)
        {
            try
            {
                return Convert.ToUInt64(value);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to convert {long} to ulong", value);
                return ulong.MinValue;
            }
        }

        /// <summary>
        /// Faker to generate random values
        /// </summary>
        internal static Faker Faker { get; private set; } = new Faker();
    }
}
