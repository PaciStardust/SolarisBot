using Discord;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;
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

        #region EmbedsV2
        /// <summary>
        /// Generates a default embed
        /// </summary>
        internal static Embed Embed(string title, string content, EmbedResponseType rt = EmbedResponseType.Default)
        {
            var color = rt switch
            {
                EmbedResponseType.Default => Color.Blue,
                EmbedResponseType.Error => Color.Red,
                _ => Color.Blue
            };

            var embed = new EmbedBuilder()
            {
                Title = title,
                Description = content,
                Color = color
            }.Build();

            return embed;
        }

        /// <summary>
        /// Generates an embed error
        /// </summary>
        internal static Embed EmbedError(string title, string content)
            => Embed(title, content, EmbedResponseType.Error);

        /// <summary>
        /// Generates an embed error based on an exception
        /// </summary>
        internal static Embed EmbedError(Exception exception)
            => Embed(exception.GetType().Name, exception.Message);

        /// <summary>
        /// Generates a generic embed error based on GET
        /// </summary>
        internal static Embed EmbedError(EmbedGenericErrorType get)
        {
            var data = get switch
            {
                EmbedGenericErrorType.NoResults => ("No Results", "Request yielded no results"),
                EmbedGenericErrorType.DatabaseError => ("Database Error", "The database encountered an error"),
                EmbedGenericErrorType.Forbidden => ("Forbidden", "You are forbidden from accessing this"),
                EmbedGenericErrorType.InvalidInput => ("Invalid Input", "Provided values are invalid"),
                _ => ("Unknown", "Unknown")
            };

            return EmbedError(data.Item1, data.Item2);
        }
        #endregion

        #region Naming
        private static readonly Regex _nameVerificator = new(@"\A[A-Za-z \d]{2,20}\Z");
        internal static bool IsIdentifierValid(string identifier)
            => _nameVerificator.IsMatch(identifier);
        #endregion
    }

    internal enum EmbedResponseType
    {
        Default,
        Error
    }

    internal enum EmbedGenericErrorType
    {
        NoResults,
        DatabaseError,
        Forbidden,
        InvalidInput
    }
}
