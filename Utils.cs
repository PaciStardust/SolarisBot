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

        static Utils()
        {
            var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();

            PathConfigFile = Path.GetFullPath(Path.Combine(assemblyDirectory, "config.json")); //todo: account for docker
            PathDatabaseFile = Path.GetFullPath(Path.Combine(assemblyDirectory, "database.db"));
            PathDictionaryFile = Path.GetFullPath(Path.Combine(assemblyDirectory, "dictionary.txt"));
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
        /// <returns>ulong value or 0, if parsing fails</returns>
        internal static ulong AsUlong(this long value, ILogger? logger = null)
        {
            try
            {
                return Convert.ToUInt64(value);
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Failed to convert {long} to ulong", value);
                return 0;
            }
        }

        /// <summary>
        /// Faker to generate random values
        /// </summary>
        internal static Faker Faker { get; private set; } = new Faker();
    }
}
