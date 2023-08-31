using System.Text.RegularExpressions;

namespace SolarisBot.Discord.UserCredibility
{
    internal class KeywordCredibilityRule : CredibilityRule
    {
        internal string Keyword { get; init; }
        private readonly Regex _regex;

        internal KeywordCredibilityRule(string name, int score, string keyword) : base(name, score)
        {
            Keyword = keyword;
            _regex = new Regex(Keyword, RegexOptions.IgnoreCase);
        }

        internal override bool Evaluate(string input)
            => _regex.IsMatch(input);
    }
}
