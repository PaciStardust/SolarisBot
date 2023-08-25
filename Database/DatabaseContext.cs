using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Database
{
    internal sealed class DatabaseContext : DbContext
    {
        private readonly ILogger<DatabaseContext> _logger;
        private static bool _hasMigrated = false;

        public DatabaseContext(DbContextOptions<DatabaseContext> options, ILogger<DatabaseContext> logger) : base(options)
        {
            _logger = logger;
            TryMigrate();
        }

        public DbSet<DbGuild> Guilds { get; set; }
        public DbSet<DbJokeTimeout> JokeTimeouts { get; set; }
        public DbSet<DbQuote> Quotes { get; set; }
        public DbSet<DbRole> Roles { get; set; }
        public DbSet<DbRoleGroup> RoleGroups { get; set; }

        /// <summary>
        /// Attempts to save changes to the database
        /// </summary>
        /// <returns>Number of changes, or -1 on error</returns>
        internal async Task<int> TrySaveChangesAsync()
        {
            try
            {
                return await SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed saving DB changes");
                return -1;
            }
        }

        #region Guilds
        /// <summary>
        /// Compiled Query for GetGuildByIdAsyn
        /// </summary>
        private static readonly Func<DatabaseContext, ulong, Task<DbGuild?>> GetGuildByIdCompiled
            = EF.CompileAsyncQuery((DatabaseContext ctx, ulong id) => ctx.Guilds.FirstOrDefault(x => x.GId == id));

        /// <summary>
        /// Get an untracked guild by Id
        /// </summary>
        /// <param name="id">Guild ID</param>
        /// <returns>Guild matching ID or null, if no match is found or an error occured</returns>
        internal async Task<DbGuild?> GetGuildByIdAsync(ulong id)
            => await GetGuildByIdCompiled(this, id);

        /// <summary>
        /// Get a tracked guild by ID
        /// </summary>
        /// <param name="id">Guild ID</param>
        /// <returns>Tracked guild matching id, or a new instance, automatically added to database, or null on error</returns>
        internal async Task<DbGuild> GetOrCreateTrackedGuildAsync(ulong id)
        {
            var guild = await Guilds.AsTracking().FirstOrDefaultAsync(x => x.GId == id);
            if (guild == null)
            {
                guild = new() { GId = id };
                await Guilds.AddAsync(guild);
            }
            return guild;
        }
        #endregion

        #region Migration
        /// <summary>
        /// Attempts to migrate the database, throws on error
        /// </summary>
        private void TryMigrate()
        {
            if (_hasMigrated) return;

            var versionQuery = Database.SqlQueryRaw<int>("PRAGMA user_version").AsEnumerable();
            var version = versionQuery.Any() ? versionQuery.FirstOrDefault() : 0;
            var migrationVersion = version;
            _logger.LogInformation("Current database version is {version}, checking for migrations", version);

            try
            {
                var transaction = Database.BeginTransaction();

                if (version < 1)
                {
                    var queries = new string[]{
                        "PRAGMA foreign_keys = ON",
                        "CREATE TABLE Guilds(GId INTEGER PRIMARY KEY, VouchRoleId INTEGER NOT NULL DEFAULT 0, VouchSpreadOn BOOL NOT NULL DEFAULT 0, CustomColorPermissionRoleId INTEGER NOT NULL DEFAULT 0, CustomColorOwnershipRoleId INTEGER NOT NULL DEFAULT 0, JokeRenameOn BOOL NOT NULL DEFAULT 0, JokeRenameTimeoutMin INTEGER NOT NULL DEFAULT 0, JokeRenameTimeoutMax INTEGER NOT NULL DEFAULT 0, MagicRoleId INTEGER NOT NULL DEFAULT 0, MagicRoleTimeout INTEGER NOT NULL DEFAULT 0, MagicRoleNextUse INTEGER NOT NULL DEFAULT 0, MagicRoleRenameOn BOOL NOT NULL DEFAULT 0, UNIQUE(VouchRoleId), UNIQUE(CustomColorPermissionRoleId), UNIQUE(CustomColorOwnershipRoleId), UNIQUE(MagicRoleId))",
                        "CREATE TABLE RoleGroups(RgId INTEGER PRIMARY KEY AUTOINCREMENT, GId INTEGER REFERENCES Guilds(GId) ON DELETE CASCADE ON UPDATE CASCADE, Name TEXT NOT NULL DEFAULT \"\", Description TEXT NOT NULL DEFAULT \"\", AllowOnlyOne BOOL NOT NULL DEFAULT 0, UNIQUE(GId, Name))",
                        "CREATE TABLE Roles(RId INTEGER PRIMARY KEY, RgId INTEGER REFERENCES RoleGroups(RgId) ON DELETE CASCADE ON UPDATE CASCADE, Name TEXT NOT NULL DEFAULT \"\", Description TEXT NOT NULL DEFAULT \"\", UNIQUE(RgId, Name))",
                        "CREATE TABLE Quotes(QId INTEGER PRIMARY KEY, GId INTEGER REFERENCES Guilds(GId) ON DELETE CASCADE ON UPDATE CASCADE, Text TEXT NOT NULL DEFAULT \"\", AuthorId INTEGER NOT NULL DEFAULT 0, Time INTEGER NOT NULL DEFAULT 0, CreatorId INTEGER NOT NULL DEFAULT 0)",
                        "CREATE TABLE JokeTimeouts(JtId INTEGER PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL DEFAULT 0, GId INTEGER REFERENCES Guilds(GId) ON DELETE CASCADE ON UPDATE CASCADE, NextUse INTEGER NOT NULL DEFAULT 0, UNIQUE(GId, UserId))"
                    };

                    foreach (var query in queries)
                        Database.ExecuteSqlRaw(query);

                    migrationVersion = 1;
                }

                if (migrationVersion > version)
                    Database.ExecuteSqlRaw($"PRAGMA user_version = {migrationVersion}");

                transaction.Commit();
                _logger.LogInformation("Database migration complete: {oldVersion} => {newVersion}", version, migrationVersion);
                _hasMigrated = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to migrate database from {originVersion} to {migrationVersion}", version, migrationVersion);
                throw;
            }
        }
        #endregion
    }
}
