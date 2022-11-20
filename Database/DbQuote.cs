using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Database
{
    internal readonly struct DbQuote
    {
        internal readonly ulong Id { get; init; } = 0;
        internal readonly string Title { get; init; } = "Unnamed Quote";
        internal readonly string Quote { get; init; } = string.Empty;
        internal readonly ulong AuthorId { get; init; } = 0;
        internal readonly string AuthorName { get; init; } = "Unknown";
        internal readonly ulong Time { get; init; } = 0;

        internal DbQuote(ulong id)
        {
            Id = id;
        }
        internal static DbQuote Default => new();

        #region Queries - Get
        /// <summary>
        /// Gets a list of all quotes matching the condition
        /// </summary>
        /// <param name="conditions">"WHERE..."</param>
        /// <param name="parameters">Sqlite Parameters</param>
        /// <returns></returns>
        internal static IReadOnlyList<DbQuote> Get(string conditions, params SqliteParameter[] parameters)
        {
            var selector = DbMain.Get("SELECT * FROM quotes WHERE " + conditions, false, parameters);

            if (selector == null || !selector.HasRows)
            {
                Logger.Debug($"GET yielded no results \"{conditions}\"");
                return new List<DbQuote>();
            }

            var quotes = new List<DbQuote>();
            while (selector.Read())
            {
                try
                {
                    quotes.Add(new()
                    {
                        Id = Convert.ToUInt64(selector.GetValue(0)),
                        Title = selector.GetString(1),
                        Quote = selector.GetString(2),
                        AuthorId = Convert.ToUInt64(selector.GetValue(3)),
                        AuthorName = selector.GetString(4),
                        Time = Convert.ToUInt64(selector.GetValue(5))
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex.GetType().Name, ex.Message);
                    if (DbMain.Run($"DELETE FROM quotes WHERE id = {selector.GetValue(0)}") < 1)
                        Logger.Warning("Invalid quote in DB could not be removed");
                }
            }
            return quotes;
        }

        /// <summary>
        /// Gets one DbQuote based on QuoteId
        /// </summary>
        /// <param name="id">QuoteId of the quote</param>
        /// <returns>Null if Quote is not id DB</returns>
        internal static DbQuote? GetOne(ulong id)
        {
            var result = Get($"id = {id}");
            return result.Count > 0 ? result[0] : null;
        }
        #endregion

        #region Queries - Set
        /// <summary>
        /// Creates an entry
        /// </summary>
        /// <param name="direct"></param>
        /// <returns>Success?</returns>
        internal bool Create()
        {
            var parameters = new List<SqliteParameter>
            {
                new("TITLE", Title),
                new("QUOTE", Quote),
                new("AUTHORID", AuthorId),
                new("AUTHORNAME", AuthorName),
                new("TIME", Time)
            };

            var paramNames = parameters.Select(x => x.ParameterName);

            //We add the id later so it doesnt cause issues (ID is autoincremented)
            //parameters.Add(new("ID", Id));

            var query = new StringBuilder("INSERT INTO quotes (");
            query.Append(string.Join(", ", paramNames));
            query.Append(") VALUES (");
            query.Append(string.Join(", ", parameters.Select(x => $"@{x.ParameterName}")));
            query.Append(')');

            if (DbMain.Run(query.ToString(), false, parameters.ToArray()) == -1)
                return false;

            return true;
        }
        #endregion
    }
}
