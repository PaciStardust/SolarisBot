namespace SolarisBot.Discord.Modules.UserAnalysis
{
    internal sealed class TimeCredibilityRule : CredibilityRule
    {
        public TimeSpan MinimumAge { get; init; }

        public TimeCredibilityRule(string name, int score, TimeSpan minimumAge) : base(name, score)
        {
            MinimumAge = minimumAge;
        }

        /// <summary>
        /// Checks if the supplied age does not match the rule
        /// </summary>
        /// <param name="age">Age to check against</param>
        /// <returns>Credible?</returns>
        internal bool IsCredible(TimeSpan age)
            => age >= MinimumAge;
    }
}
