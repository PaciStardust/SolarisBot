using Newtonsoft.Json;
using System.Reflection;
using System.Text;

namespace SolarisBot
{
    internal static class Config
    {
        internal static ConfigModel Data { get; private set; }
        internal static ConfigLoggerModel Logging => Data.Logging;
        internal static ConfigDiscordModel Discord => Data.Discord;
        internal static ConfigCommandModel Command => Data.Command;

        internal static string ResourcePath { get; private set; }
        internal static string ConfigPath { get; private set; }
        internal static string DatabasePath { get; private set; }
        internal static string LogPath { get; private set; }

        #region Saving and Loading
        static Config()
        {
            var assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? Directory.GetCurrentDirectory();

            ResourcePath = Path.GetFullPath(Path.Combine(assemblyDirectory, "config"));
            ConfigPath = Path.GetFullPath(Path.Combine(ResourcePath, "config.json"));
            DatabasePath = Path.GetFullPath(Path.Combine(ResourcePath, "database.db"));
            LogPath = Path.GetFullPath(Path.Combine(ResourcePath, "log.txt"));

            try
            {
                if (!Directory.Exists(ResourcePath))
                    Directory.CreateDirectory(ResourcePath);

                string configData = File.ReadAllText(ConfigPath, Encoding.UTF8);
                Data = JsonConvert.DeserializeObject<ConfigModel>(configData) ?? new();
            }
            catch
            {
                Data = GetFirstConfig();
                SaveConfig();
            }
        }

        /// <summary>
        /// Creates a config using data requested from the user via console
        /// </summary>
        internal static ConfigModel GetFirstConfig()
        {
            var newConfig = new ConfigModel();

            newConfig.Discord.Token = AskQuestion("What is the bot token?");

            while(true)
            {
                if (ulong.TryParse(AskQuestion("What is the main guild id?"), out var parsed))
                {
                    newConfig.Discord.MainGuild = parsed;
                    break;
                }
            }

            return newConfig;
        }

        /// <summary>
        /// Saves the config file
        /// </summary>
        internal static void SaveConfig()
        {
            try
            {
                File.WriteAllText(ConfigPath, JsonConvert.SerializeObject(Data ?? new(), Formatting.Indented));
                Logger.Info("Saved config file at " + ConfigPath);
            }
            catch (Exception e)
            {
                Logger.Error(e, "The config file was unable to be saved.");
            }
        }
        #endregion

        #region Utility
        /// <summary>
        /// Makes sure the given value does not exceed bounds
        /// </summary>
        /// <typeparam name="T">Type, must be a comparable</typeparam>
        /// <param name="value">Value to compare</param>
        /// <param name="min">Lower bound</param>
        /// <param name="max">Upper bound</param>
        /// <returns>New value</returns>
        private static T MinMax<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(max) > 0) return max;
            if (value.CompareTo(min) < 0) return min;
            return value;
        }

        /// <summary>
        /// Asks the user a question in console
        /// </summary>
        /// <param name="question">Question to be asked</param>
        /// <returns>Response given by user</returns>
        private static string AskQuestion(string question)
        {
            string? reply = null;

            while(string.IsNullOrWhiteSpace(reply))
            {
                Console.Write($"> {question} >>> ");
                reply = Console.ReadLine() ?? string.Empty;
            }

            return reply;
        }

        //public static string GetVersion() //Todo: version checking
        //{
        //    var assembly = System.Reflection.Assembly.GetEntryAssembly();
        //    return "v." + (assembly != null ? FileVersionInfo.GetVersionInfo(assembly.Location).FileVersion : "Version Unknown");
        //}

        #endregion

        #region Models
        /// <summary>
        /// Model for storing all config data
        /// </summary>
        internal class ConfigModel
        {
            public ConfigLoggerModel Logging { get; init; } = new();
            public ConfigDiscordModel Discord { get; init; } = new();
            public ConfigCommandModel Command { get; init; } = new();
        }

        /// <summary>
        /// Model for all Logging related data
        /// </summary>
        internal class ConfigLoggerModel
        {
            public bool Error { get; set; } = true;
            public bool Warning { get; set; } = true;
            public bool Info { get; set; } = true;
            public bool Verbose { get; set; } = true;
            public bool Critical { get; set; } = true;
            public bool Debug { get; set; } = false;
            public List<string> LogFilter { get; set; } = new();
        }

        /// <summary>
        /// Model for all Discord related data
        /// </summary>
        internal class ConfigDiscordModel
        {
            public bool StatupBot { get; set; } = true;
            public string Token { get; set; } = string.Empty;
            public ulong MainGuild { get; set; } = 0;
        }
        #endregion

        /// <summary>
        /// Model for all command configuration
        /// </summary>
        internal class ConfigCommandModel
        {
            public uint MaxQuotesPerPerson { get; set; } = 100;
            public bool TagQuoteIfPossible { get; set; } = false;
        }
    }
}