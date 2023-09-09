using Newtonsoft.Json;
using System.Text;

namespace SolarisBot
{
    internal sealed class BotConfig
    {
        #region Loading, Saving
        /// <summary>
        /// Loads the BotConfig 
        /// </summary>
        /// <param name="path">Path to load from</param>
        /// <returns>Config, null if unavailable</returns>
        internal static BotConfig? FromFile(string path)
        {
            if (!File.Exists(path))
                return null;

            try
            {
                string configData = File.ReadAllText(path, Encoding.UTF8);
                return JsonConvert.DeserializeObject<BotConfig>(configData);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Saves the BotConfig
        /// </summary>
        /// <param name="path">Path to save at</param>
        /// <returns>Success?n</returns>
        internal bool SaveAt(string path)
        {
            try
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(this ?? new(), Formatting.Indented));
                return true;
            }
            catch
            {
                return false;
            }
        }
        #endregion

        #region Data
        public string Token { get; set; } = string.Empty;
        public ulong MainGuild { get; set; } = 0;
        public string DefaultStatus { get; set; } = "SolarisBot by Paci";
        public bool GlobalLoad { get; set; } = false;
        public byte MaxRemindersPerUser { get; set; } = 16;
        public short MaxQuoteCharacters { get; set; } = 500;
        public byte MaxQuotesPerUser { get; set; } = 32; //Per server
        #endregion
    }
}
