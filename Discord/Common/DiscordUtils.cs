using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using SolarisBot.Discord.Common;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Common
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
        internal static string Log(this IUser user)
            => $"{user.GlobalName}({user.Id})";

        /// <summary>
        /// Returns Name and Id of a Guild for logging
        /// </summary>
        internal static string Log(this IGuild guild)
            => $"{guild.Name}({guild.Id})";

        /// <summary>
        /// Returns a formatted version of Log for use in embeds
        /// </summary>
        internal static string ToDiscordInfoString(this IGuild guild)
            => $"**{guild.Name}**​ *({guild.Id})*";

        /// <summary>
        /// Returns Name and Id of a Role for logging
        /// </summary>
        internal static string Log(this IRole role)
            => $"{role.Name}({role.Id})";

        /// <summary>
        /// Returns Name and Id of a Channel for logging
        /// </summary>
        internal static string Log(this IChannel channel)
            => $"{channel.Name}({channel.Id})";

        /// <summary>
        /// Returns a formatted version of Log for use in embeds
        /// </summary>
        internal static string ToDiscordInfoString(this IChannel channel)
            => $"**{channel.Name}** *({channel.Id})*";
        #endregion

        #region Naming
        private static readonly Regex _nameVerificator = new(@"\A[A-Za-z \d]{2,20}\Z");
        internal static bool IsIdentifierValid(string identifier)
            => _nameVerificator.IsMatch(identifier);
        #endregion

        #region Roles
        internal const string CustomColorRolePrefix = "Solaris Custom Color";

        internal static string GetCustomColorRoleName(IUser user)
            => $"{CustomColorRolePrefix} {user.Id}";

        /// <summary>
        /// Gets a role by ID
        /// </summary>
        /// <param name="id">ID of role</param>
        /// <returns>A role matching the ID or null if none could be found</returns>
        internal static SocketRole? FindRole(this SocketGuildUser gUser, ulong id)
            => gUser.Roles.FirstOrDefault(x => x.Id == id);
        #endregion
    }
}
