using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Discord.UserCredibility
{
    internal abstract class CredibilityRule
    {
        internal string Name { get; init; }
        internal int Score { get; init; }

        internal abstract bool Evaluate(string input); //todo: return a result?

        internal CredibilityRule(string name, int score)
        {
            Name = name;
            Score = score;
        }
    }
}
