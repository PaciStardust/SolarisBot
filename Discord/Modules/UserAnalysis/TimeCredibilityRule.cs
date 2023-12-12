namespace SolarisBot.Discord.Modules.UserAnalysis
{
    internal sealed class TimeCredibilityRule : CredibilityRule
    {
        public TimeSpan MinimumAge { get; init; }

        internal TimeCredibilityRule(string name, int score, TimeSpan minimumAge) : base(name, score)
        {
            MinimumAge = minimumAge;
        }

        internal bool IsCredible(DateTime dateTime)
            => DateTime.Now - dateTime >= MinimumAge;
    }
}
