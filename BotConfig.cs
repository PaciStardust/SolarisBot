using Newtonsoft.Json;
using SolarisBot.Discord.Modules.UserAnalysis;
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

        internal void Update()
        {
            if (Version < 1)
            {
                var credibilityKeywords = new KeywordCredibilityRule[]
                {
                    new("Firstname Lastname", -10, @"\A[a-z]{2,}(?:[-_ ]+[a-z]{2,})+\Z"),
                    new("Randomly Generated", -100, @"\A[a-z]{2,}\d*(?:[-_ ]+[a-z\d]+)*[-_ ]+\d+\Z"),
                    new("Name with many Numbers", -50, @"\A.+(?:\d{3}|\d{5,})\Z"),
                    new("Legacy Convert", -10, @"\A.+\d{4}\Z"),
                    new("Design", -10, "design(?:s|er)?"),
                    new("2D/3D", -10, "[23]d"),
                    new("Artistry", -10, "art(?:ist|s)?"),
                    new("Model", -10, "(?:av(?:atar|i)s?|modell?(?:s|er|ing)?)"),
                    new("V-Tuber", -10, "v[- ]*tub(?:er|ing)"),
                    new("Studio", -25, "studio"),
                    new("Graphics", -10,"(?:gfx|graphics?|textur(?:e|ing))"),
                    new("Animation", -10, "animat(?:or|ion)"),
                    new("Streaming", -10, "(?:stream(?:er|ing)?|twitch|t?tv)"), //this one idk about
                    new("Rigging", -15, "rigg(?:ing|er)"),
                    new("Commission", -50, "comm(?:issions?)"),
                    new("DM Me", -50, "(?:(?:dm|msg|message)[- ]*me|direct[- ]*(?:messages?|mgs))"),
                    new("Fiverr Link", -35, "fiverr"),
                    new("VRoid", -15, "(?:vroid|vrm)"),
                    new("Custom", -50, @"\bcustom\b"),
                    new("Giveaway Link", -75, @"(?:raffle|giveaway|\bfree\b)"),
                    new("Skin", -30, "skins?"),
                    new("Discord Link", -20, @"(?:discord|dsc)\.gg"),
                    new("Hacked", -50, "(?:hacked|compromised)"),
                    new("Alt Account", -50, "(?:alt(?:ernative)?[- ]*(?:acc(?:ount)?)?|new[- ]*acc(?:ount)?)"),
                    new("Portfolio", -15, "(?:portfolio|pric(?:es|ing))"),
                    new("Emotes", -30,"emo(?:tes?|ji)"),
                    new("Profile Picture", -10, "(?:profile[- ]*pic(?:ture)?|pfp)s?"),
                    new("Banner", -10, "banners?"),
                    new("Logo", -20, "logos?"),
                    new("Linktree/Carrd", -5, @"(?:carrd|linktr)"), //-5 for now per request
                    new("Artstation", -10, "artstation"),
                    new("2/3D Artistry Bonus", -10, "[23]d[- ]*(?:art(?:ist|s)?|model(?:s|er|ing)?)"),
                    new("V-Tuber Artistry Bonus", -10, "v[- ]*tuber[- ]*(?:design(?:s|er)|art(?:ist)?)"),
                    new("Model Artistry Bonus", -10, "(?:av(?:atar|i)|model)[- ]*art(?:ist)?"),
                    new("Graphics Artistry Bonus", -10, "(?:gfx|graphics?|texture)[- ]*(?:design(?:s|er)?|artist)"),
                    new("Stream Artistry Bonus", -10, "(?:stream(?:er|ing)?|twitch|t?tv)[- ]*emo(?:tes?|ji)")
                };
                CredibilityRulesKeyword.AddRange(credibilityKeywords);

                var credibilityTimes = new TimeCredibilityRule[]
                {
                    new("Age <= 1D", -500, new(1, 0, 0, 0)),
                    new("Age <= 5D", -450, new(5, 0, 0, 0)),
                    new("Age <= 15D", -400, new(15, 0, 0, 0)),
                    new("Age <= 30D", -300, new(30, 0, 0, 0)),
                    new("Age <= 90D", -200, new(90, 0, 0, 0)),
                    new("Age <= 180D", -150, new(180, 0, 0, 0)),
                    new("Age <= 270D", -75, new(270, 0, 0, 0)),
                    new("Age <= 360D", -35, new(360, 0, 0, 0)),
                };
                //todo: time

                Version = 1;
            }
        }
        #endregion

        #region Data
        public ulong Version { get; set; } = 0;
        public string Token { get; set; } = string.Empty;
        public ulong MainGuild { get; set; } = ulong.MinValue;
        public string DefaultStatus { get; set; } = "SolarisBot by Paci";
        public byte MaxRemindersPerUser { get; set; } = 16;
        public short MaxQuoteCharacters { get; set; } = 500;
        public byte MaxQuotesPerUser { get; set; } = 32; //Per server
        public ulong MaxReminderTimeOffset { get; set; } = 60 * 60 * 24 * 366 * 4; //Roughly four years
        public string[] DisabledModules { get; set; } = Array.Empty<string>();
        public byte MaxImageSizeInBytes { get; set; } = 8;
        public byte MaxBridgesPerGuild { get; set; } = 8;
        public List<TimeCredibilityRule> CredibilityRulesTime { get; set; } = new();
        public List<KeywordCredibilityRule> CredibilityRulesKeyword { get; set; } = new();
        #endregion
    }
}
