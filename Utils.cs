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
        internal static string PathMainDirectory { get; private set; }

        static Utils() //todo: [REFACTOR] Fix this mess
        {
            PathMainDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
            PathConfigDirectory = Path.Combine(PathMainDirectory, "cfg");
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

        /// <summary>
        /// Converts a string into a Ulong
        /// </summary>
        /// <param name="text">Ulong to convert</param>
        /// <returns>Ulong if parsed, otherwise null</returns>
        internal static ulong? ToUlongOrNull(string? text)
        {
            var success = ulong.TryParse(text, out var parsedId);
            return success ? parsedId : null;
        }
    }
}
