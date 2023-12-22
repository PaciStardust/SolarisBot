using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Modules.UserAnalysis
{
    internal sealed class KeywordCredibilityRule : CredibilityRule
    {
        public string Keyword { get; init; }
        private readonly Regex _regex;

        public KeywordCredibilityRule(string name, int score, string keyword) : base(name, score)
        {
            Keyword = keyword;
            _regex = new Regex(Keyword, RegexOptions.IgnoreCase);
        }

        internal bool IsCredible(string text)
            => !_regex.IsMatch(text);
    }
}
