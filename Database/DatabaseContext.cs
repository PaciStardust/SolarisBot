﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Database
{
    internal sealed class DatabaseContext : DbContext
    {
        private readonly ILogger<DatabaseContext> _logger;
        private static bool _hasMigrated = false;

        public DatabaseContext(DbContextOptions<DatabaseContext> options, ILogger<DatabaseContext> logger) : base(options) //Todo: [OPTIMIZE] Use select statements, better updates?
        {
            _logger = logger;
            TryMigrate();
        }

        public DbSet<DbGuildConfig> GuildConfigs { get; set; }
        public DbSet<DbJokeTimeout> JokeTimeouts { get; set; }
        public DbSet<DbQuote> Quotes { get; set; }
        public DbSet<DbRoleConfig> RoleConfigs { get; set; }
        public DbSet<DbRoleGroup> RoleGroups { get; set; }
        public DbSet<DbReminder> Reminders { get; set; }
        public DbSet<DbBridge> Bridges { get; set; }
        public DbSet<DbRegexChannel> RegexChannels { get; set; }

        /// <summary>
        /// Attempts to save changes to the database
        /// </summary>
        /// <returns>Number of changes, or -1 on error</returns>
        internal async Task<(int, Exception?)> TrySaveChangesAsync()
        {
            try
            {
                return (await SaveChangesAsync(), null);
            }
            catch (Exception ex)
            {
                return (-1, ex);
            }
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var changedEntries = ChangeTracker.Entries().Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);
            if (changedEntries.Any())
            {
                var now = Utils.GetCurrentUnix();
                foreach (var entry in changedEntries)
                {
                    if (entry.Entity is not DbModelBase baseEntity)
                        continue;

                    if (entry.State == EntityState.Added)
                        baseEntity.CreatedAt = now;
                    baseEntity.UpdatedAt = now;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }

        #region Migration
        /// <summary>
        /// Attempts to migrate the database, throws on error
        /// </summary>
        private void TryMigrate() //todo: [FEATURE] code first?
        {
            if (_hasMigrated) return;

            var versionQuery = Database.SqlQueryRaw<int>("PRAGMA user_version").AsEnumerable();
            var version = versionQuery.Any() ? versionQuery.FirstOrDefault() : 0;
            var migrationVersion = version;
            _logger.LogInformation("Current database version is {version}, checking for migrations", version);

            try
            {
                var transaction = Database.BeginTransaction();
                var queries = new List<string>();

                if (version < 1)
                {
                    queries.AddRange(new List<string>()
                    {
                        "PRAGMA foreign_keys = ON",

                        "CREATE TABLE GuildConfigs(GuildId INTEGER PRIMARY KEY, VouchRoleId INTEGER NOT NULL DEFAULT 0, VouchPermissionRoleId INTEGER NOT NULL DEFAULT 0, CustomColorPermissionRoleId INTEGER NOT NULL DEFAULT 0, JokeRenameOn BOOL NOT NULL DEFAULT 0, JokeRenameTimeoutMin INTEGER NOT NULL DEFAULT 0, JokeRenameTimeoutMax INTEGER NOT NULL DEFAULT 0, MagicRoleId INTEGER NOT NULL DEFAULT 0, MagicRoleTimeout INTEGER NOT NULL DEFAULT 0, MagicRoleNextUse INTEGER NOT NULL DEFAULT 0, MagicRoleRenameOn BOOL NOT NULL DEFAULT 0, RemindersOn BOOL NOT NULL DEFAULT 0, QuotesOn BOOL NOT NULL DEFAULT 0, AutoRoleId INTEGER NOT NULL DEFAULT 0, SpellcheckRoleId INTEGER NOT NULL DEFAULT 0, StealNicknameOn BOOL NOT NULL DEFAULT 0, GififyOn BOOL NOT NULL DEFAULT 0, QuarantineRoleId INTEGER NOT NULL DEFAULT 0, UserAnalysisChannelId INTEGER NOT NULL DEFAULT 0, UserAnalysisWarnAt INTEGER NOT NULL DEFAULT 0, UserAnalysisKickAt INTEGER NOT NULL DEFAULT 0, UserAnalysisBanAt INTEGER NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT 0, UpdatedAt INTEGER NOT NULL DEFAULT 0)",

                        "CREATE TABLE RoleGroups(RoleGroupId INTEGER PRIMARY KEY AUTOINCREMENT, GuildId INTEGER REFERENCES GuildConfigs(GuildId) ON DELETE CASCADE ON UPDATE CASCADE, Identifier TEXT NOT NULL DEFAULT \"\", Description TEXT NOT NULL DEFAULT \"\", AllowOnlyOne BOOL NOT NULL DEFAULT 0, RequiredRoleId INTEGER NOT NULL DEFAULT 0, IsDeleted BOOL NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT 0, UpdatedAt INTEGER NOT NULL DEFAULT 0, UNIQUE(GuildId, Identifier))",
                        
                        "CREATE TABLE RoleConfigs(RoleId INTEGER PRIMARY KEY, RoleGroupId INTEGER REFERENCES RoleGroups(RoleGroupId) ON DELETE CASCADE ON UPDATE CASCADE, Identifier TEXT NOT NULL DEFAULT \"\", Description TEXT NOT NULL DEFAULT \"\", IsDeleted BOOL NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT 0, UpdatedAt INTEGER NOT NULL DEFAULT 0, UNIQUE(RoleGroupId, Identifier))",
                        
                        "CREATE TABLE Quotes(QuoteId INTEGER PRIMARY KEY, GuildId INTEGER REFERENCES GuildConfigs(GuildId) ON DELETE CASCADE ON UPDATE CASCADE, Text TEXT NOT NULL DEFAULT \"\", AuthorId INTEGER NOT NULL DEFAULT 0, CreatorId INTEGER NOT NULL DEFAULT 0, ChannelId INTEGER NOT NULL DEFAULT 0, MessageId INTEGER NOT NULL DEFAULT 0, IsDeleted BOOL NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT 0, UpdatedAt INTEGER NOT NULL DEFAULT 0, UNIQUE(MessageId), UNIQUE(AuthorId, GuildId, Text))",
                        
                        "CREATE TABLE JokeTimeouts(JokeTimeoutId INTEGER PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL DEFAULT 0, GuildId INTEGER REFERENCES GuildConfigs(GuildId) ON DELETE CASCADE ON UPDATE CASCADE, NextUse INTEGER NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT 0, UpdatedAt INTEGER NOT NULL DEFAULT 0, UNIQUE(GuildId, UserId))",
                        
                        "CREATE TABLE Reminders(ReminderId INTEGER PRIMARY KEY AUTOINCREMENT, UserId INTEGER NOT NULL DEFAULT 0, GuildId INTEGER REFERENCES GuildConfigs(GuildId) ON DELETE CASCADE ON UPDATE CASCADE, ChannelId INTEGER NOT NULL DEFAULT 0, RemindAt INTEGER NOT NULL DEFAULT 0, Text TEXT NOT NULL DEFAULT \"\", IsDeleted BOOL NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT 0, UpdatedAt INTEGER NOT NULL DEFAULT 0, UNIQUE(GuildId, UserId, Text))",
                        
                        "CREATE TABLE Bridges(BridgeId INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL DEFAULT \"\", GuildAId INTEGER NOT NULL DEFAULT 0, ChannelAId INTEGER NOT NULL DEFAULT 0, GuildBId INTEGER NOT NULL DEFAULT 0, ChannelBId INTEGER NOT NULL DEFAULT 0, IsDeleted BOOL NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT 0, UpdatedAt INTEGER NOT NULL DEFAULT 0, CHECK(ChannelAId <> ChannelBId))",
                        "CREATE TRIGGER BridgesAvoidDuplicateInsert BEFORE INSERT ON Bridges BEGIN SELECT RAISE(ABORT, 'Duplicate bridge on insert') WHERE EXISTS (SELECT 1 FROM Bridges WHERE (NEW.IsDeleted = 0 AND IsDeleted = 0 AND ((ChannelAId = NEW.ChannelAId AND ChannelBId = NEW.ChannelBId) OR (ChannelBId = NEW.ChannelAId AND ChannelAId = NEW.ChannelBId)))); END;",
                        "CREATE TRIGGER BridgesAvoidDuplicateUpdate BEFORE UPDATE ON Bridges BEGIN SELECT RAISE(ABORT, 'Duplicate bridge on update') WHERE EXISTS (SELECT 1 FROM Bridges WHERE (NEW.IsDeleted = 0 AND IsDeleted = 0 AND ((ChannelAId = NEW.ChannelAId AND ChannelBId = NEW.ChannelBId) OR (ChannelBId = NEW.ChannelAId AND ChannelAId = NEW.ChannelBId)))); END;",
                        "CREATE TRIGGER BridgesSoftDelete BEFORE DELETE ON Bridges FOR EACH ROW BEGIN UPDATE Bridges SET IsDeleted = 1 WHERE BridgeId = OLD.BridgeId; END;",

                        "CREATE TABLE RegexChannels(ChannelId INTEGER PRIMARY KEY, GuildId REFERENCES GuildConfigs(GuildId) ON DELETE CASCADE ON UPDATE CASCADE, Regex TEXT NULL DEFAULT \"\", Punishment INTEGER NOT NULL DEFAULT 0, PunishmentValue INTEGER NOT NULL DEFAULT 0, PunishmentMessage TEXT NOT NULL DEFAULT \"\", PunishmentDelete BOOL NOT NULL DEFAULT 0, IsDeleted BOOL NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT 0, UpdatedAt INTEGER NOT NULL DEFAULT 0"
                    });
                    migrationVersion = 1;
                }

                if (queries.Count > 0)
                {
                    foreach (var query in queries)
                        Database.ExecuteSqlRaw(query);
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
