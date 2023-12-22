namespace SolarisBot.Discord.Modules.UserAnalysis
{
    internal abstract class CredibilityRule
    {
        public string Name { get; init; }
        public int Score { get; init; }

        public CredibilityRule(string name, int score)
        {
            Name = name;
            Score = score;
        }

        public override string ToString()
            => $"{Name} *({Score})*";
    }
}
