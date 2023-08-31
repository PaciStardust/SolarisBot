using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SolarisBot.Discord.UserCredibility
{
    internal class UserCredibilityEvaluator
    {
        private readonly Dictionary<string, int> _results = new();

        internal UserCredibilityEvaluator(IUser user)
        {

        }

        /// <summary>
        /// Keywords for Name only
        /// </summary>
        private static readonly List<KeywordCredibilityRule> _nameKeywords = new()
        {
            new("Firstname Lastname", -10, @"\A[a-z]{2,}(?:[-_ ]+[a-z]{2,})+\Z"),
            new("Randomly Generated", -100, @"\A[a-z]{2,}\d*(?:[-_ ]+[a-z\d]+)*[-_ ]+\d+\Z"),
            new("Name with many Numbers", -50, @"\A.+(?:\d{3}|\d{5,})\Z"),
            new("Legacy Convert", -10, @"\A.+\d{4}\Z")
        };

        /// <summary>
        /// Keywords for Name and Description
        /// </summary>
        private static readonly List<KeywordCredibilityRule> _keywords = new()
        {
            new("Design", -10, "design(?:s|er)?"),
            new("3D", -10, "3d"),
            new("Artistry", -10, "art(?:ist|s)?"),
            new("Model", -10, "(?:av(?:atar|i)s?|model(?:s|er|ing)?)"),
            new("V-Tuber", -10, "v[- ]*tub(?:er|ing)"),
            new("Studio", -25, "studio"),
            new("Graphics", -10,"(?:gfx|graphics?|textur(?:e|ing))")
        };

        /// <summary>
        /// Keywords for Description only
        /// </summary>
        private static readonly List<KeywordCredibilityRule> _descriptionKeywords = new()//todo: with boundary, hacked, alt, new account -> 50
        {
            new("Commission", -50, "comm(?:issions?)"),
            new("DM Me", -50, "(?:(?:dm|msg|message)[- ]*me|direct[- ]*(?:messages?|mgs))"),
            new("Fiverr Link", -35, "fiverr"),
            new("VRoid", -15, "vroid"),
            new("Rigging", -15, "rigging"),
            new("Custom", -50, "custom"),
            new("Giveaway Link", -75, "(?:raffle|giveaway|free)"),
            new("Skin", -30, "skins?"),
            new("Discord Link", -20, @"(?:discord|dsc)\.gg")
        };

        /// <summary>
        /// Bonus descriptionkeywords that combine others
        /// </summary>
        private static readonly List<KeywordCredibilityRule> _extraBonusDescriptionKeywords = new()
        {
            new("3D Artistry Bonus", -10, "3d[- ]*(?:art(?:ist|sz)?|model(?:s|er|ing)?)"),
            new("V-Tuber Artistry Bonus", -10, "v[- ]*tuber[- ]*(?:design(?:s|er)|art(?:ist)?)"),
            new("Model Artistry Bonus", -10, "(?:av(?:atar|i)|model)[- ]*art(?:ist)?"),
            new("Graphics Artistry Bonus", -10, "(?:gfx|graphics?|texture)[- ]*(?:design(?:s|er)|artist)"),
        };
    }
}

