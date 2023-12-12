namespace SolarisBot.Discord.Modules.UserAnalysis
{
    internal class CredibilityRule
    {
        public string Name { get; init; }
        public int Score { get; init; }

        internal CredibilityRule(string name, int score)
        {
            Name = name;
            Score = score;
        }
    }
}
