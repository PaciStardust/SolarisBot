using Bogus;
using System.Reflection;

namespace SolarisBot
{
    internal static class Utils
    {
        internal static string PathConfigFile { get; private set; }
        internal static string PathConfigDirectory { get; private set; }

        static Utils()
        {
            var mainDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();
            PathConfigDirectory = Path.Combine(mainDirectory, "cfg");
            PathConfigFile = Path.GetFullPath(Path.Combine(PathConfigDirectory, "config.json"));
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
