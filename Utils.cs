using System.Reflection;

namespace SolarisBot
{
    internal static class Utils
    {
        internal static string PathConfigFile { get; private set; }
        internal static string PathDatabaseFile { get; private set; }

        static Utils()
        {
            var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();

            PathConfigFile = Path.GetFullPath(Path.Combine(assemblyDirectory, "config.json"));
            PathDatabaseFile = Path.GetFullPath(Path.Combine(assemblyDirectory, "database.db"));
        }

        /// <summary>
        /// Gets current Unix as ULong
        /// </summary>
        /// <returns>Current Uunix Timestamp (seconds)</returns>
        internal static ulong GetCurrentUnix()
            => LongToUlong(DateTimeOffset.Now.ToUnixTimeSeconds());

        /// <summary>
        /// Converts a long value to a ulong value
        /// </summary>
        /// <param name="value">Long to convert</param>
        /// <returns>ulong value or 0, if parsing fails</returns>
        internal static ulong LongToUlong(this long value)
        {
            try
            {
                return Convert.ToUInt64(value);
            }
            catch
            {
                //todo: [LOGGING] log error?
                //Logger.Error(ex);
                return 0;
            }
        }
    }
}
