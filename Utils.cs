using Bogus;
using Microsoft.Extensions.Logging;
using Serilog.Core;
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
        /// <returns>Current Unix Timestamp (seconds)</returns>
        internal static ulong GetCurrentUnix()
            => Convert.ToUInt64(DateTimeOffset.Now.ToUnixTimeSeconds());

        /// <summary>
        /// Faker to generate random values
        /// </summary>
        internal static Faker Faker { get; private set; } = new Faker();
    }
}
