using Discord;
using System.Runtime.CompilerServices;
using System.Text;

namespace SolarisBot
{
    /// <summary>
    /// Class for logging of information in console
    /// </summary>
    internal static class Logger
    {
        private static readonly StreamWriter? _logWriter;
        private static readonly object _lock; //Used for writer

        static Logger()
        {
            _lock = new(); //we need to create the lock first so that it initializes correctly
            _logWriter = StartLogger();
        }

        #region Logger Start

        /// <summary>
        /// Starts the logging service
        /// </summary>
        /// <returns>Streamwriter for logging</returns>
        private static StreamWriter? StartLogger()
        {
            try
            {
                var writer = File.CreateText(Config.LogPath);
                writer.AutoFlush = true;
                Info("Created logging file at " + Config.LogPath);
                return writer;
            }
            catch (Exception e)
            {
                Error(e);
                return null;
            }
        }
        #endregion

        #region Logging Function
        internal static void Log(LogMessage message)
        {
            if (!LogLevelAllowed(message.Severity)) return;

            lock (_lock) //Making sure log writing is not impacted by multithreading
            {
                var actualMessage = message.Message ?? message.Exception.Message ?? string.Empty;

                if (string.IsNullOrWhiteSpace(actualMessage))
                    return;

                if (Config.Logging.LogFilter.Count != 0)
                {
                    var lowerMessage = actualMessage.ToLower();
                    foreach (var filter in Config.Logging.LogFilter)
                        if (lowerMessage.Contains(filter))
                            return;
                }

                var logSeverity = message.Severity.ToString();
                if (logSeverity.Length > _maxSeverityLength)
                    logSeverity = logSeverity[.._maxSeverityLength];
                logSeverity = logSeverity.PadRight(_maxSeverityLength, ' ');

                var messageString = $"{DateTime.Now:HH:mm:ss.fff} {logSeverity} {actualMessage.Replace("\n", " ").Replace("\r", "")}";

                Console.ForegroundColor = GetLogColor(message.Severity);
                Console.WriteLine(messageString);
                _logWriter?.WriteLine(messageString);
            }
        }

        private static readonly int _maxSeverityLength = 8;
        internal static void Log(string message, LogSeverity severity = LogSeverity.Verbose, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
        {
            var logLocation = $"{Path.GetFileName(file)}::{member}:{line}";
            var logMessage = $"[{logLocation}] {message}";

            Log(new(severity, string.Empty, logMessage));
        }

        //NORMAL LOGGING
        internal static void Debug(string message, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            => Log(message, LogSeverity.Debug, file, member, line);
        internal static void Info(string message, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            => Log(message, LogSeverity.Info, file, member, line);
        internal static void Warning(string message, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0)
            => Log(message, LogSeverity.Warning, file, member, line);

        // ERROR LOGGING
        internal static void Error(string message, bool silent = false, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0) //Error with basic message
        {
            var severity = silent ? LogSeverity.Debug : LogSeverity.Error;
            Log(message, severity, file, member, line);
        }

        internal static void Error(Exception error, string descriptiveError = "", bool silent = false, [CallerFilePath] string file = "", [CallerMemberName] string member = "", [CallerLineNumber] int line = 0) //Error using exception
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(descriptiveError))
                sb.AppendLine(descriptiveError + "\n\n--------------------------\nError details:\n");

            sb.AppendLine(error.Message);

            if (error.InnerException != null)
                sb.AppendLine($"\n(Inner {error.InnerException.GetType()}: {error.Message}{(error.Source == null ? "" : $" at {error.Source}")})");

            if (error.StackTrace != null)
                sb.AppendLine("\n--------------------------\nStack trace:\n\n" + error.StackTrace);

            Error(sb.ToString(), silent, file, member, line);
        }
        #endregion

        #region Utils
        /// <summary>
        /// Get the color of the log message for the log window
        /// </summary>
        /// <param name="severity">Severity of log</param>
        /// <returns>Corresponding log color</returns>
        private static ConsoleColor GetLogColor(LogSeverity severity) => severity switch
        {
            LogSeverity.Critical => ConsoleColor.DarkRed,
            LogSeverity.Error => ConsoleColor.Red,
            LogSeverity.Warning => ConsoleColor.Yellow,
            LogSeverity.Info => ConsoleColor.Cyan,
            LogSeverity.Verbose => ConsoleColor.White,
            LogSeverity.Debug => ConsoleColor.DarkGray,
            _ => ConsoleColor.DarkGray
        };

        /// <summary>
        /// Check if logging is allowed for severity
        /// </summary>
        /// <param name="severity">Severity of log</param>
        /// <returns>Logging enabled?</returns>
        private static bool LogLevelAllowed(LogSeverity severity) => severity switch
        {
            LogSeverity.Critical => Config.Logging.Error,
            LogSeverity.Error => Config.Logging.Error,
            LogSeverity.Warning => Config.Logging.Warning,
            LogSeverity.Info => Config.Logging.Info,
            LogSeverity.Verbose => Config.Logging.Verbose,
            LogSeverity.Debug => Config.Logging.Debug,
            _ => false
        };
        #endregion
    }
}