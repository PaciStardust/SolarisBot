using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Database
{
    internal readonly struct DbVcGen
    {
        internal readonly ulong VChannel { get; init; } = 0;
        internal readonly ulong TChannel { get; init; } = 0;
        internal readonly ulong Owner { get; init; } = 0;

        internal DbVcGen(ulong vchan, ulong tchan, ulong owner)
        {
            VChannel = vchan;
            TChannel = tchan;
            Owner = owner;
        }
        internal static DbVcGen Default => new();

        #region Queries - Get
        /// <summary>
        /// Gets a list of vcgen matching the condition
        /// </summary>
        /// <param name="conditions">"WHERE..."</param>
        /// <param name="parameters">Sqlite Parameters</param>
        /// <returns></returns>
        internal static IReadOnlyList<DbVcGen> Get(string conditions, params SqliteParameter[] parameters)
        {
            var selector = DbMain.Get("SELECT * FROM vcgen WHERE " + conditions, false, parameters);

            if (selector == null || !selector.HasRows)
            {
                Logger.Debug($"GET yielded no results \"{conditions}\"");
                return new List<DbVcGen>();
            }

            var quotes = new List<DbVcGen>();
            while (selector.Read())
            {
                try
                {
                    quotes.Add(new()
                    {
                        VChannel = Convert.ToUInt64(selector.GetValue(0)),
                        TChannel = Convert.ToUInt64(selector.GetValue(1)),
                        Owner = Convert.ToUInt64(selector.GetValue(2))
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex.GetType().Name, ex.Message);
                    if (DbMain.Run($"DELETE FROM vcgen WHERE vchannel = {selector.GetValue(0)}") < 1)
                        Logger.Warning("Invalid vcgen in DB could not be removed");
                }
            }
            return quotes;
        }

        /// <summary>
        /// Gets all vcgen of one guild
        /// </summary>
        /// <param name="id">GuildId of the guild</param>
        /// <returns>Null if Guild is not id DB</returns>
        internal static IReadOnlyList<DbVcGen> GetOne(ulong vcid)
        {
            var result = Get($"vchannel = {vcid}");
            return result;
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
                new("VCHANNEL", VChannel),
                new("TCHANNEL", TChannel),
                new("OWNER", Owner)
            };

            var paramNames = parameters.Select(x => x.ParameterName);

            var query = new StringBuilder("INSERT INTO vcgen (")
                .Append(string.Join(", ", paramNames))
                .Append(") VALUES (")
                .Append(string.Join(", ", parameters.Select(x => $"@{x.ParameterName}")))
                .Append(')');

            if (DbMain.Run(query.ToString(), false, parameters.ToArray()) == -2)
                return false;

            return true;
        }
        #endregion
    }
}
