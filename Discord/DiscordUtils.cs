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

        /// <summary>
        /// Returns GlobalName and Id of a User for logging
        /// </summary>
        internal static string GetLogInfo(this IUser user)
            => $"{user.GlobalName}({user.Id})";

        /// <summary>
        /// Returns Name and Id of a Guild for logging
        /// </summary>
        internal static string GetLogInfo(this IGuild guild)
            => $"{guild.Name}({guild.Id})";

        /// <summary>
        /// Returns Name and Id of a Role for logging
        /// </summary>
        internal static string GetLogInfo(this IRole role)
            => $"{role.Name}({role.Id})";
        #endregion

        #region EmbedsV2
        /// <summary>
        /// Generates a default embedbuilder
        /// </summary>
        internal static EmbedBuilder EmbedBuilder(string title, Color? colorOverride = null)
            => new()
            {
                Title = title,
                Color = colorOverride ?? Color.Blue
            };

        /// <summary>
        /// Generates a default embed
        /// </summary>
        internal static Embed Embed(string title, string content, Color? colorOverride = null)
            => EmbedBuilder(title, colorOverride)
                .WithDescription(content)
                .Build();

        /// <summary>
        /// Generates an embed error
        /// </summary>
        internal static Embed EmbedError(string title, string content)
            => Embed(title, content, Color.Red);

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

    internal enum EmbedGenericErrorType
    {
        NoResults,
        DatabaseError,
        Forbidden,
        InvalidInput
    }
}
