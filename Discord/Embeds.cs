using Discord;
using Discord.Interactions;
using System.Runtime.CompilerServices;

namespace SolarisBot.Discord
{
    internal static class Embeds
    {
        #region Constants
        internal static Color ColorImportant => new(235, 52, 229);
        internal static Color ColorDefault => new(52, 155, 235);
        #endregion

        #region Default Embeds
        /// <summary>
        /// Creates a simple info embed
        /// </summary>
        /// <param name="title">Title of embed</param>
        /// <param name="message">Message of embed</param>
        /// <returns>Created embed</returns>
        internal static Embed Info(string title, string message) => new EmbedBuilder()
        {
            Title = title,
            Description = message,
            Color = ColorDefault
        }.Build();
        /// <summary>
        /// Creates a simple info embed
        /// </summary>
        /// <param name="title">Title of embed</param>
        /// <param name="message">Message of embed</param>
        /// <param name="color">Color of embed</param>
        /// <returns>Created embed</returns>
        internal static Embed Info(string title, string message, Color color) => new EmbedBuilder()
        {
            Title = title,
            Description = message,
            Color = color
        }.Build();

        internal static Embed InvalidInput => Info("Invalid input", "The input you have given is invalid");
        internal static Embed GuildOnly => Info("Guild only", "Command requires usage to be done within a guild");
        internal static Embed NoResult => Info("No result", "The request yielded no result in the database");
        #endregion

        #region Error Embeds
        /// <summary>
        /// Automatically logs the error
        /// </summary>
        internal static Embed Error(string title, string message, bool log = true, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            if (log)
                Logger.Error(message, false, file, member, line);

            return new EmbedBuilder()
            {
                Title = title,
                Description = message,
                Color = Color.Red
            }.Build();
        }
        /// <summary>
        /// Automatically logs the error
        /// </summary>
        internal static Embed Error(Exception error, string descriptiveError = "", [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            Logger.Error(error, descriptiveError, false, file, member, line);

            return new EmbedBuilder()
            {
                Title = error.GetType().Name,
                Description = error.Message,
                Color = Color.Red
            }.Build();
        }

        internal static Embed DbFailure => Error("Database Error", "The database encountered an error during a request", false);
        #endregion
    }
}
