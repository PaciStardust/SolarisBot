using Microsoft.Data.Sqlite;
using Newtonsoft.Json.Linq;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace SolarisBot.Database
{
    internal static class DbMain
    {
        private static readonly SqliteConnection? _connection;

        internal static int ExecutedRun { get; private set; } = 0;
        internal static int ExecutedGet { get; private set; } = 0;

        static DbMain()
        {
            try
            {
                var dbPath = Config.DatabasePath;
                if (!File.Exists(dbPath))
                {
                    Logger.Info("Database file does not exist, creating it");
                    File.Create(dbPath).Dispose();
                }

                var conn = new SqliteConnection($"Data Source={dbPath}");
                conn.Open();
                _connection = conn;

                if (!UpgradeDatabase())
                    _connection = null;
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
            }
        }

        #region Queries - Main
        /// <summary>
        /// Performs a GET Query on the database
        /// </summary>
        /// <param name="query">The query to be performed</param>
        /// <param name="silent">Log error as debug if available</param>
        /// <param name="parameters">Sqlite parameters</param>
        /// <returns>Number of changes, -1 on error</returns>
        internal static int Run(string query, bool silent = true, params SqliteParameter[] parameters)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                Logger.Debug("Empty query received, skipping");
                return -1;
            }

            var command = new SqliteCommand(query, _connection);
            command.Parameters.AddRange(parameters);

            var parameterString = SummarizeSqlParameters(parameters);
            try
            {
                var lines = command.ExecuteNonQuery();
                Logger.Debug($"Performed RUN Query \"{command.CommandText}\" ({parameterString}) ({lines} changes)");
                ExecutedRun++;
                return lines;
            }
            catch (Exception e)
            {
                Logger.Error($"Encountered a {e.GetType().FullName} perfoming RUN Query \"{command.CommandText}\" ({parameterString}): {e.Message}", silent);
                return -1;
            }
        }

        /// <summary>
        /// Performs a GET Query on the database
        /// </summary>
        /// <param name="query">The query to be performed</param>
        /// <param name="silent">Log error as debug if available</param>
        /// <param name="parameters">Sqlite parameters</param>
        /// <returns>An SqliteDataReader or null, if an error occured</returns>
        internal static SqliteDataReader? Get(string query, bool silent = true, params SqliteParameter[] parameters)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                Logger.Debug("Empty query received, skipping");
                return null;
            }

            var command = new SqliteCommand(query, _connection);
            command.Parameters.AddRange(parameters);

            var parameterString = SummarizeSqlParameters(parameters);
            try
            {
                var reader = command.ExecuteReader();
                Logger.Debug($"Performed GET Query \"{command.CommandText}\" ({parameterString})");
                ExecutedGet++;
                return reader;
            }
            catch (Exception e)
            {
                Logger.Error($"Encountered a {e.GetType().FullName} perfoming GET Query \"{command.CommandText}\" ({parameterString}): {e.Message}", silent);
                return null;
            }
        }
        #endregion

        #region Queries - Utility
        /// <summary>
        /// Updates a single value in a table
        /// </summary>
        /// <param name="table">Name of table</param>
        /// <param name="idName">Name of identifier</param>
        /// <param name="idValue">Value of identifier</param>
        /// <param name="changeName">Name of column</param>
        /// <param name="changeValue">Value of column</param>
        /// <param name="silent">Will error in log?</param>
        /// <returns></returns>
        internal static bool SetOne(string table, string changeName, object? changeValue, string idName, object? idValue, bool silent = true)
        {
            if (!Sanitized(table) || !Sanitized(changeName) || !Sanitized(idName))
                return false;

            var parameters = new SqliteParameter[]
            {
                new("IDVALUE", idValue),
                new("CHANGEVALUE", changeValue)
            };

            return Run($"UPDATE {table} SET {changeName} = @CHANGEVALUE WHERE {idName} = @IDVALUE", silent, parameters) > 0;
        }
        #endregion

        #region Queries - Value Setting
        /// <summary>
        /// Sets a value in the uniques database
        /// </summary>
        /// <param name="id">Identifier for the value</param>
        /// <param name="value">Value to be set</param>
        /// <returns>Success?</returns>
        internal static bool SetValue(string id, string value)
        {
            if (!SetOne("uniques", "value", value, "id", id))
            {
                if (Run($"INSERT INTO uniques (id, value) VALUES ({id}, {value})", true) == -1)
                    return false;
            }

            Logger.Log($"Set value \"{id}\" to \"{value}\"");
            return true;
        }

        /// <summary>
        /// Gets a value from the uniques database
        /// </summary>
        /// <param name="id">Identifier of the value</param>
        /// <returns>The value as string or null if the id lead to no results</returns>
        internal static string? GetValue(string id)
        {
            var selector = Get($"SELECT value FROM uniques WHERE id = @ID", true, new SqliteParameter("ID", id));
            if (selector != null && selector.HasRows)
            {
                while (selector.Read())
                {
                    return selector.GetString(0);
                }
            }

            return null;
        }
        #endregion

        #region DB Upgrade 
        private static readonly Func<int>[] _upgradeFunctions = 
        {
            /*0*/   new(() => Run("CREATE TABLE guilds (id INT PRIMARY KEY NOT NULL CHECK(id >= 0), renaming BOOL NOT NULL DEFAULT 0, magicrole INT CHECK(magicrole >= 0), magicrename BOOL NOT NULL DEFAULT 0, magictimeout INT NOT NULL DEFAULT 1800 CHECK(magictimeout >= 0), magiclast INT NOT NULL DEFAULT 0 CHECK(magiclast >= 0))", false)),
            /*1*/   new(() => Run("ALTER TABLE guilds ADD vouchrole INT CHECK(vouchrole >= 0); ALTER TABLE guilds ADD vouchuser BOOL NOT NULL DEFAULT 0"))
        };

        private static bool UpgradeDatabase()
        {
            //Making sure uniques table exists
            if (Run("CREATE TABLE IF NOT EXISTS uniques (id TEXT NOT NULL PRIMARY KEY, value TEXT)", false) == -1)
                return false;

            var versionString = GetValue("version");
            var versionNumber = -1;

            if (versionString != null && int.TryParse(versionString, out var vNum))
                versionNumber = vNum;
            else
            {
                if (Run("INSERT INTO uniques (id, value) VALUES ('version', '-1')", false) == -1)
                    return false;
            }

            int newVer = _upgradeFunctions.Length - 1;
            Logger.Info($"Database is version {versionNumber}, newest is {newVer}");

            for (int i = versionNumber + 1; i <= newVer; i++)
            {
                if (_upgradeFunctions[i].Invoke() < 1)
                {
                    Logger.Info("Failed to update database to version " + i);
                    return false;
                }
                else
                    Logger.Info("Updated database to version " + i);
            }

            if (!SetValue("version", newVer.ToString()))
                Logger.Warning("Updated database but unable to save new version");

            return true;
        }
        #endregion

        #region Utils
        internal static Random Random => new();
        internal static ulong GetCurrentUnix()
            => Convert.ToUInt64(Math.Max(0, DateTimeOffset.Now.ToUnixTimeSeconds()));

        private static readonly Regex _validator = new("[a-zA-Z0-9_]+");
        internal static bool Sanitized(string input)
            => _validator.IsMatch(input);

        internal static string SummarizeSqlParameters(params SqliteParameter[] parameters)
            => string.Join(", ", parameters.Select(x => $"{x.ParameterName}={x.Value}"));

        private static readonly string _letters = "aeiouybcdfghjklmnpqrstvwxz";
        /// <summary>
        /// Returns a randomly generated name
        /// </summary>
        /// <param name="minSyl">Minimum amount of syllables</param>
        /// <param name="maxSyl">Maximum amount of syllables</param>
        /// <returns>Name</returns>
        internal static string GetName(int minSyl, int maxSyl)
        {
            var syllables = Random.Next(minSyl, maxSyl+1);
            var name = new StringBuilder();

            for(int i = 0; i < syllables; i++)
            {
                var firstLetter = _letters[Random.Next(0, _letters.Length)];
                var secondLetter = _letters[Random.Next(0, 5)]; //Only vowels

                if (i == 0)
                    name.Append(firstLetter.ToString().ToUpper() + secondLetter);
                else
                    name.Append(firstLetter.ToString() + secondLetter);

                if (Random.Next(0,2) == 1)
                    name.Append(_letters[Random.Next(0, _letters.Length)]);
            }
            
            return name.ToString();
        }
        #endregion
    }
}
