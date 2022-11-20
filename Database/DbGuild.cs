using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Database
{
    internal readonly struct DbGuild
    {
        internal readonly ulong Id { get; init; } = 0;
        internal readonly bool Renaming { get; init; } = false;
        internal readonly ulong? MagicRole { get; init; } = null;
        internal readonly bool MagicRename { get; init; } = false;
        internal readonly ushort MagicTimeout { get; init; } = 1800;
        internal readonly ulong MagicLast { get; init; } = 0;
        internal readonly ulong? VouchRole { get; init; } = null;
        internal readonly bool VouchUser { get; init; } = false;

        internal DbGuild(ulong id)
        {
            Id = id;
        }
        internal static DbGuild Default => new();

        #region Queries - Get
        /// <summary>
        /// Gets a list of all DdGuilds matching the condition
        /// </summary>
        /// <param name="conditions">"WHERE..."</param>
        /// <param name="parameters">Sqlite Parameters</param>
        /// <returns></returns>
        internal static IReadOnlyList<DbGuild> Get(string conditions, params SqliteParameter[] parameters)
        {
            var selector = DbMain.Get("SELECT * FROM guilds WHERE " + conditions, false, parameters);

            if (selector == null || !selector.HasRows)
            {
                Logger.Debug($"GET yielded no results \"{conditions}\"");
                return new List<DbGuild>();
            }

            var guilds = new List<DbGuild>();
            while(selector.Read())
            {
                try
                {
                    guilds.Add(new()
                    {
                        Id = Convert.ToUInt64(selector.GetValue(0)),
                        Renaming = selector.GetBoolean(1),
                        MagicRole = selector.IsDBNull(2) ? null : Convert.ToUInt64(selector.GetValue(2)),
                        MagicRename = selector.GetBoolean(3),
                        MagicTimeout = Convert.ToUInt16(selector.GetValue(4)),
                        MagicLast = Convert.ToUInt64(selector.GetValue(5)),
                        VouchRole = selector.IsDBNull(6) ? null : Convert.ToUInt64(selector.GetValue(6)),
                        VouchUser = selector.GetBoolean(7)
                    });
                }
                catch (Exception ex)
                {
                    Logger.Warning(ex.GetType().Name, ex.Message);
                    if (DbMain.Run($"DELETE FROM guilds WHERE id = {selector.GetValue(0)}") < 1)
                        Logger.Warning("Invalid guildSetting in DB could not be removed");
                }
            }
            return guilds;
        }

        /// <summary>
        /// Gets one DbGuild based on GuildId
        /// </summary>
        /// <param name="id">GuildId of the guild</param>
        /// <returns>Null if Guild is not id DB</returns>
        internal static DbGuild? GetOne(ulong id)
        {
            var result = Get($"id = {id}");
            return result.Count > 0 ? result[0] : null;
        }
        #endregion

        #region Queries - Set
        /// <summary>
        /// Changes one Value
        /// </summary>
        /// <param name="changeName">Value to change</param>
        /// <param name="changeValue">Value</param>
        /// <param name="id">Guild ID</param>
        /// <param name="silent">Logged as error on fail</param>
        /// <returns>Success?</returns>
        internal static bool SetOne(string changeName, object? changeValue, ulong id, bool silent = true)
            => DbMain.SetOne("guilds", changeName, changeValue, "id", id, silent);

        /// <summary>
        /// Creates an entry
        /// </summary>
        /// <param name="direct"></param>
        /// <returns>Success?</returns>
        internal bool Create()
        {
            var parameters = new List<SqliteParameter>
            {
                new("RENAMING", Renaming),
                new("MAGICLAST", MagicLast),
                new("MAGICTIMEOUT", MagicTimeout),
                new("MAGICRENAME", MagicRename),
                new("VOUCHUSER", VouchUser)
            };
            if (MagicRole != null)
                parameters.Add(new("MAGICROLE", MagicRole));
            if (VouchRole != null)
                parameters.Add(new("VOUCHROLE", VouchRole));

            var paramNames = parameters.Select(x => x.ParameterName);

            //We add the id later so it doesnt cause issues
            parameters.Add(new("ID", Id));

            var query = new StringBuilder ("INSERT INTO guilds (");
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
