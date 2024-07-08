using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Database
{
    internal class DatabaseService
    {
        private readonly DbContextOptions<DatabaseContext> _options;
        private readonly ILogger<DatabaseService> _logger;
        private bool _initialized = false;

        public DatabaseService(BotConfig botConfig, ILogger<DatabaseService> logger)
        {
            _logger = logger;
            var dbPath = Path.Combine(Utils.PathConfigDirectory, botConfig.DatabaseFile);
            var options = new DbContextOptionsBuilder<DatabaseContext>()
                .UseSqlite($"Data Source={dbPath};Pooling=false");
            _options = options.Options;
        }

        /// <summary>
        /// Initializes the service and migrates the DB
        /// </summary>
        internal async Task ReadyAsync()
        {
            if (_initialized) return;

            using var dbCtx = CreateContextInternal();
            await MigrateAsync(dbCtx);
            _initialized = true;
        }

        private DatabaseContext CreateContextInternal()
            => new(_options);

        /// <summary>
        /// Returns a new Database Context, do not forget to use "Using"
        /// </summary>
        /// <returns>New Context</returns>
        internal DatabaseContext GetContext()
        {
            if (!_initialized)
                throw new InvalidOperationException("Unable to create DatabaseContext before DbService has been initialized");

            return CreateContextInternal();
        }

        #region Migration
        /// <summary>
        /// Migrates the DB
        /// </summary>
        private async Task MigrateAsync(DatabaseContext dbCtx)
        {
            var versionQuery = await dbCtx.Database.SqlQueryRaw<int>("PRAGMA user_version").ToArrayAsync();
            var version = versionQuery.Length > 0 ? versionQuery[0] : 0;
            var migrationVersion = version;
            _logger.LogInformation("Current database version is {version}, checking for migrations", version);

            try
            {
                _logger.LogInformation($"Beginning SQL update transaction");
                using var transaction = await dbCtx.Database.BeginTransactionAsync();
                var queries = new List<string>();

                if (version < 1)
                {
                    queries.AddRange(new List<string>()
                    {
                        "PRAGMA foreign_keys = ON",

                        "CREATE TABLE GuildConfigs(GuildId INTEGER PRIMARY KEY NOT NULL, VouchRoleId INTEGER NOT NULL DEFAULT 0, VouchPermissionRoleId INTEGER NOT NULL DEFAULT 0, VouchMessage TEXT NOT NULL DEFAULT \"\", CustomColorPermissionRoleId INTEGER NOT NULL DEFAULT 0, CustomColorIndicator TEXT NOT NULL DEFAULT \"\", JokeRenameOn BOOL NOT NULL DEFAULT 0, JokeRenameTimeoutMin INTEGER NOT NULL DEFAULT 0, JokeRenameTimeoutMax INTEGER NOT NULL DEFAULT 0, MagicRoleId INTEGER NOT NULL DEFAULT 0, MagicRoleTimeout INTEGER NOT NULL DEFAULT 0, MagicRoleNextUse INTEGER NOT NULL DEFAULT 0, MagicRoleRenameOn BOOL NOT NULL DEFAULT 0, RemindersOn BOOL NOT NULL DEFAULT 0, QuotesOn BOOL NOT NULL DEFAULT 0, AutoRoleId INTEGER NOT NULL DEFAULT 0, SpellcheckRoleId INTEGER NOT NULL DEFAULT 0, StealNicknameOn BOOL NOT NULL DEFAULT 0, GififyOn BOOL NOT NULL DEFAULT 0, QuarantineRoleId INTEGER NOT NULL DEFAULT 0, UserAnalysisChannelId INTEGER NOT NULL DEFAULT 0, UserAnalysisWarnAt INTEGER NOT NULL DEFAULT 0, UserAnalysisKickAt INTEGER NOT NULL DEFAULT 0, UserAnalysisBanAt INTEGER NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UpdatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')))",
                        "CREATE TRIGGER GuildConfigsSetModified AFTER UPDATE ON GuildConfigs FOR EACH ROW BEGIN UPDATE GuildConfigs SET UpdatedAt = strftime('%s', 'now') WHERE GuildId = NEW.GuildId; END",

                        "CREATE TABLE RoleGroups(RoleGroupId INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, GuildId INTEGER REFERENCES GuildConfigs(GuildId) ON DELETE CASCADE ON UPDATE CASCADE, Identifier TEXT NOT NULL DEFAULT \"\", Description TEXT NOT NULL DEFAULT \"\", AllowOnlyOne BOOL NOT NULL DEFAULT 0, RequiredRoleId INTEGER NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UpdatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UNIQUE(GuildId, Identifier))",
                        "CREATE TRIGGER RoleGroupsSetModified AFTER UPDATE ON RoleGroups FOR EACH ROW BEGIN UPDATE RoleGroups SET UpdatedAt = strftime('%s', 'now') WHERE RoleGroupId = NEW.RoleGroupId; END",

                        "CREATE TABLE RoleConfigs(RoleConfigId INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, RoleId INTEGER NOT NULL DEFAULT 0, RoleGroupId INTEGER REFERENCES RoleGroups(RoleGroupId) ON DELETE CASCADE ON UPDATE CASCADE, Identifier TEXT NOT NULL DEFAULT \"\", Description TEXT NOT NULL DEFAULT \"\", CreatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UpdatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UNIQUE(RoleId), UNIQUE(RoleGroupId, Identifier))",
                        "CREATE TRIGGER RoleConfigsSetModified AFTER UPDATE ON RoleConfigs FOR EACH ROW BEGIN UPDATE RoleConfigs SET UpdatedAt = strftime('%s', 'now') WHERE RoleConfigId = NEW.RoleConfigId; END",

                        "CREATE TABLE Quotes(QuoteId INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, GuildId INTEGER REFERENCES GuildConfigs(GuildId) ON DELETE CASCADE ON UPDATE CASCADE, Text TEXT NOT NULL DEFAULT \"\", AuthorId INTEGER NOT NULL DEFAULT 0, CreatorId INTEGER NOT NULL DEFAULT 0, ChannelId INTEGER NOT NULL DEFAULT 0, MessageId INTEGER NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UpdatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UNIQUE(MessageId), UNIQUE(AuthorId, GuildId, Text))",
                        "CREATE TRIGGER QuotesSetModified AFTER UPDATE ON Quotes FOR EACH ROW BEGIN UPDATE Quotes SET UpdatedAt = strftime('%s', 'now') WHERE QuoteId = NEW.QuoteId; END",

                        "CREATE TABLE JokeTimeouts(JokeTimeoutId INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, UserId INTEGER NOT NULL DEFAULT 0, GuildId INTEGER REFERENCES GuildConfigs(GuildId) ON DELETE CASCADE ON UPDATE CASCADE, NextUse INTEGER NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UpdatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UNIQUE(GuildId, UserId))",
                        "CREATE TRIGGER JokeTimeoutsSetModified AFTER UPDATE ON JokeTimeouts FOR EACH ROW BEGIN UPDATE JokeTimeouts SET UpdatedAt = strftime('%s', 'now') WHERE JokeTimeoutId = NEW.JokeTImeoutId; END",

                        "CREATE TABLE Reminders(ReminderId INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, UserId INTEGER NOT NULL DEFAULT 0, GuildId INTEGER REFERENCES GuildConfigs(GuildId) ON DELETE CASCADE ON UPDATE CASCADE, ChannelId INTEGER NOT NULL DEFAULT 0, RemindAt INTEGER NOT NULL DEFAULT 0, Text TEXT NOT NULL DEFAULT \"\", CreatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UpdatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UNIQUE(GuildId, UserId, Text))",
                        "CREATE TRIGGER RemindersSetModified AFTER UPDATE ON Reminders FOR EACH ROW BEGIN UPDATE Reminders SET UpdatedAt = strftime('%s', 'now') WHERE ReminderId = NEW.ReminderId; END",

                        "CREATE TABLE Bridges(BridgeId INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, Name TEXT NOT NULL DEFAULT \"\", GuildAId INTEGER NOT NULL DEFAULT 0, ChannelAId INTEGER NOT NULL DEFAULT 0, GuildBId INTEGER NOT NULL DEFAULT 0, ChannelBId INTEGER NOT NULL DEFAULT 0, AllowLinks BOOL NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UpdatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UNIQUE(ChannelAId, ChannelBId), CHECK(ChannelAId <> ChannelBId))",
                        "CREATE TRIGGER BridgesSetModified AFTER UPDATE ON Bridges FOR EACH ROW BEGIN UPDATE Bridges SET UpdatedAt = strftime('%s', 'now') WHERE BridgeId = NEW.BridgeId; END",

                        "CREATE TABLE RegexChannels(RegexChannelId INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, GuildId INTEGER NOT NULL DEFAULT 0, ChannelId INTEGER NOT NULL DEFAULT 0, Regex TEXT NULL DEFAULT \"\", AppliedRoleId INTEGER NOT NULL DEFAULT 0, PunishmentMessage TEXT NOT NULL DEFAULT \"\", PunishmentDelete BOOL NOT NULL DEFAULT 0, PunishmentTimeout INTEGER NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UpdatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UNIQUE(ChannelId))",
                        "CREATE TRIGGER RegexChannelsSetModified AFTER UPDATE ON RegexChannels FOR EACH ROW BEGIN UPDATE RegexChannels SET UpdatedAt = strftime('%s', 'now') WHERE RegexChannelId = NEW.RegexChannelId; END",

                        "CREATE TABLE CustomColorRoles(CustomColorRoleId INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, GuildId INTEGER REFERENCES GuildConfigs(GuildId), RoleId INTEGER NOT NULL DEFAULT 0, UserId INTEGER NOT NULL DEFAULT 0, CreatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UpdatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UNIQUE(RoleId), UNIQUE(GuildId, UserId))",
                        "CREATE TRIGGER CustomColorRolesSetModified AFTER UPDATE ON CustomColorRoles FOR EACH ROW BEGIN UPDATE CustomColorRoles SET UpdatedAt = strftime('%s', 'now') WHERE CustomColorRoleId = NEW.CustomColorRoleId; END",

                        "CREATE TABLE VouchActions(VouchActionId INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, GuildId INTEGER REFERENCES GuildConfigs(GuildId), ExecutingUserId INTEGER NOT NULL DEFAULT 0, TargetUserId INTEGER NOT NULL DEFAULT 0, VouchedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), CreatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UpdatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')))",
                        "CREATE TRIGGER VouchActionsSetModified AFTER UPDATE ON VouchActions FOR EACH ROW BEGIN UPDATE VouchActions SET UpdatedAt = strftime('%s', 'now') WHERE VouchActionId = NEW.VouchActionId; END",

                        "CREATE TABLE InteractionRecords(InteractionRecordId INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, InteractionCreatedAt INTEGER NOT NULL DEFAULT 0, InteractionCompletedAt INTEGER NOT NULL DEFAULT 0, InteractionId INTEGER NOT NULL DEFAULT 0, InteractionType TEXT NOT NULL DEFAULT \"\", ContextType TEXT NOT NULL DEFAULT \"\", Success BOOL NOT NULL DEFAULT 0, ModuleName TEXT NOT NULL DEFAULT \"\", GuildId INTEGER NOT NULL DEFAULT 0, ChannelId INTEGER NOT NULL DEFAULT 0, UserId INTEGER NOT NULL DEFAULT 0, CommandGroupName TEXT NOT NULL DEFAULT \"\", CommandName TEXT NOT NULL DEFAULT \"\", MethodName TEXT NOT NULL DEFAULT \"\", Arguments TEXT NOT NULL DEFAULT \"\", ErrorType TEXT NOT NULL DEFAULT \"\", ErrorMessage TEXT NOT NULL DEFAULT \"\", ErrorTrace TEXT NOT NULL DEFAULT \"\", CreatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UpdatedAt INTEGER NOT NULL DEFAULT (strftime('%s', 'now')), UNIQUE(InteractionId))",
                        "CREATE TRIGGER InteractionRecordsSetModified AFTER UPDATE ON InteractionRecords FOR EACH ROW BEGIN UPDATE InteractionRecords SET UpdatedAt = strftime('%s', 'now') WHERE InteractionRecordId = NEW.InteractionRecordId; END",
                    });
                    migrationVersion = 1;
                }

                if (queries.Count > 0)
                {
                    foreach (var query in queries)
                    {
                        _logger.LogInformation("Adding {query} to SQL update transaction", query);
                        await dbCtx.Database.ExecuteSqlRawAsync(query);
                        _logger.LogInformation("Added {query} to SQL update transaction", query);
                    }

                }

                if (migrationVersion > version)
                {
                    _logger.LogInformation("Setting user_version of database to {version} in SQL update transaction", migrationVersion);
                    var query = $"PRAGMA user_version = {migrationVersion}";
                    await dbCtx.Database.ExecuteSqlRawAsync(query);
                    _logger.LogInformation("Set user_version of database to {version} in SQL update transaction", migrationVersion);
                }

                _logger.LogInformation($"Comitting SQL update transaction");
                await transaction.CommitAsync();
                _logger.LogInformation("Database migration complete: {oldVersion} => {newVersion}", version, migrationVersion);
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
