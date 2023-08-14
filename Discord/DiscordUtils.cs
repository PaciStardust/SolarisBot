using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using Color = Discord.Color;

namespace SolarisBot.Discord
{
    internal static class DiscordUtils
    {
        #region Extention Methods
        /// <summary>
        /// Converts a discord log message and logs it using ILogger
        /// </summary>
        /// <param name="logMessage">Discord log message</param>
        /// <param name="logger">Logger used for logging</param>
        /// <returns>Task</returns>
        internal static Task Log<T>(this LogMessage logMessage, ILogger<T> logger)
        {
            LogLevel logLevel = logMessage.Severity switch
            {
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Info => LogLevel.Information,
                LogSeverity.Debug => LogLevel.Debug,
                LogSeverity.Verbose => LogLevel.Trace,
                _ => LogLevel.Debug
            };
            logger.Log(logLevel, logMessage.Exception, "Discord - {message}", logMessage.Message);
            return Task.CompletedTask;
        }
        #endregion

        /// <summary>
        /// Generates a response embed
        /// </summary>
        /// <param name="title">Title of embed</param>
        /// <param name="content">Content text of embed</param>
        #region Embeds
        internal static Embed ResponseEmbed(string title, string content)
            => new EmbedBuilder()
                {
                    Title = title,
                    Description = content,
                    Color = Color.Blue
                }.Build();

        /// <summary>
        /// Generates an error embed
        /// </summary>
        /// <param name="title">Title of embed</param>
        /// <param name="content">Content text of embed</param>
        internal static Embed ErrorEmbed(string title, string content)
            => new EmbedBuilder()
                {
                    Title = title,
                    Description = content,
                    Color = Color.Red
                }.Build();
        /// <summary>
        /// Generates an error embed
        /// </summary>
        /// <param name="exception">Exception to respond with</param>
        internal static Embed ErrorEmbed(Exception exception)
            => ErrorEmbed(exception.GetType().Name, exception.Message);
        /// <summary>
        /// Generates a generic "No results" error embed
        /// </summary>
        internal static Embed NoResultsEmbed()
            => ErrorEmbed("No Results", "Request yielded no results");
        /// <summary>
        /// Generates a generic "Database Error" embed
        /// </summary>
        internal static Embed DatabaseErrorEmbed()
            => ErrorEmbed("Database Error", "The database encountered an error");
        #endregion
    }
}
