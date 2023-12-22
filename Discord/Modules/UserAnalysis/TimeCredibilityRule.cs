namespace SolarisBot.Discord.Modules.UserAnalysis
{
    internal sealed class TimeCredibilityRule : CredibilityRule
    {
        public TimeSpan MinimumAge { get; init; }

        public TimeCredibilityRule(string name, int score, TimeSpan minimumAge) : base(name, score)
        {
            MinimumAge = minimumAge;
        }

        internal bool IsCredible(TimeSpan age)
            => age >= MinimumAge;
    }
}
