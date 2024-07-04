using Discord;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Quotes
{
    [Module("quotes"), AutoLoadService]
    internal class QuoteService
    {
        private readonly ILogger<QuoteService> _logger;
        private readonly DatabaseService _dbService;
        private readonly BotConfig _botConfig;

        public QuoteService(ILogger<QuoteService> logger, DatabaseService dbService, BotConfig botConfig)
        {
            _logger = logger;
            _botConfig = botConfig;
            _dbService = dbService;
        }

        #region Commands
        /// <summary>
        /// Create a quote
        /// </summary>
        /// <param name="targetMessage">Message to quote</param>
        /// <param name="sourceGuild">Guild of Quote</param>
        /// <param name="executingUser">Quoting user</param>
        /// <returns>Quote on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbQuote>, Error<string>, Error<Exception>>> CreateQuoteAsync(IMessage targetMessage, IGuild sourceGuild, IUser executingUser)
        {
            var msgLen = targetMessage.CleanContent.Length;
            if (msgLen > _botConfig.MaxQuoteCharacters)
                return new Error<string>($"Message is too long to quote, message has **{msgLen - _botConfig.MaxQuoteCharacters}** characters too many *(Max is {_botConfig.MaxQuoteCharacters})*");

            using var dbCtx = _dbService.GetContext();
            var guild = await dbCtx.GetGuildByIdAsync(sourceGuild.Id);
            if (guild is null || !guild.QuotesOn)
                return new Error<string>(StandardError.DisabledFeature("Quotes"));

            //Check for duplicates
            if (await dbCtx.Quotes.ForGuild(sourceGuild.Id).AnyAsync(x => x.MessageId == targetMessage.Id || x.AuthorId == targetMessage.Author.Id && x.Text == targetMessage.CleanContent && x.GuildId == sourceGuild.Id))
                return new Error<string>("Message has already been quoted");

            //Check if user has available slots
            if ((await dbCtx.Quotes.ForGuild(sourceGuild.Id).CountAsync(x => x.CreatorId == executingUser.Id)) >= _botConfig.MaxQuotesPerUser)
                return new Error<string>($"You already have **{_botConfig.MaxQuotesPerUser}** Quotes on this server, please delete some to create more");

            var dbQuote = new DbQuote()
            {
                AuthorId = targetMessage.Author.Id,
                ChannelId = targetMessage.Channel.Id,
                CreatorId = executingUser.Id,
                GuildId = sourceGuild.Id,
                MessageId = targetMessage.Id,
                Text = targetMessage.CleanContent,
                CreatedAt = Utils.GetCurrentUnix()
            };

            _logger.LogDebug("Adding quote {quote} by user {user} to guild {guild}", dbQuote, executingUser.Log(), sourceGuild.Log());
            dbCtx.Quotes.Add(dbQuote);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed adding quote {quote} by user {user} to guild {guild}", dbQuote, executingUser.Log(), sourceGuild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Added quote {quote} by user {user} to guild {guild}", dbQuote, executingUser.Log(), sourceGuild.Log());
            return new Success<DbQuote>(dbQuote);
        }

        /// <summary>
        /// Deletes a quote by id
        /// </summary>
        /// <param name="executingUser">User deleting the quote</param>
        /// <param name="quoteId">Id of quote to delete</param>
        /// <returns>Deleted quote on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbQuote>, Error<string>, Error<Exception>>> DeleteQuoteByIdAsync(IUser executingUser, ulong quoteId)
        {
            if (executingUser is not IGuildUser executingGuildUser)
                return new Error<string>(StandardError.FailedConversion("executing user", "GuildUser"));
            bool isAdmin = executingGuildUser.GuildPermissions.ManageMessages;

            using var dbCtx = _dbService.GetContext();
            var dbQuote = await dbCtx.Quotes.FirstOrDefaultAsync(x => x.QuoteId == quoteId && (x.AuthorId == executingUser.Id || x.CreatorId == executingUser.Id || isAdmin && executingGuildUser.Guild.Id == x.GuildId));
            if (dbQuote is null)
                return new Error<string>(StandardError.NoResults);

            _logger.LogDebug("Removing quote {quote}", dbQuote);
            dbCtx.Quotes.Remove(dbQuote);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed removing quote {quote}", dbQuote);
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Removed quote {quote}", dbQuote);
            return new Success<DbQuote>(dbQuote);
        }

        /// <summary>
        /// Searches for quotes within a guild
        /// </summary>
        /// <param name="guildId">Id of Guild</param>
        /// <param name="authorId">Id of Author</param>
        /// <param name="creatorId">Id of Creator</param>
        /// <param name="quoteId">Id of Quote</param>
        /// <param name="content">Content of Quote</param>
        /// <param name="offset">Search offset</param>
        /// <param name="showFirst">Only show first result, otherwise 10</param>
        /// <returns>Search result</returns>
        internal Task<DbQuote[]> SearchQuotesForGuildAsync(ulong guildId, ulong? authorId, ulong? creatorId, ulong? quoteId, string? content, int offset, bool showFirst)
            => GetQuotesAsync(guildId, authorId: authorId, creatorId: creatorId, quoteId: quoteId, content: content, offset: offset, limit: showFirst ? 1 : 10);

        /// <summary>
        /// Search for quotes created by a user
        /// </summary>
        /// <param name="userId">Id of User</param>
        /// <param name="authorId">Id of Author</param>
        /// <param name="quoteId">Id of Quote</param>
        /// <param name="content">Content of Quote</param>
        /// <param name="offset">Search offset</param>
        /// <returns>Search result</returns>
        internal Task<DbQuote[]> SearchQuotesForUserAsync(ulong userId, ulong? authorId, ulong? quoteId, string? content, int offset)
            => GetQuotesAsync(0, creatorId: userId, authorId: authorId, quoteId: quoteId, content: content, offset: offset);

        /// <summary>
        /// Grabs a random quote from the quild
        /// </summary>
        /// <param name="guildId">Id of Guild</param>
        /// <returns>Quote on success / None</returns>
        internal async Task<OneOf<Success<DbQuote>, Error<string>>> GetRandomQuoteAsync(ulong guildId)
        {
            using var dbCtx = _dbService.GetContext();
            var quotesQuery = dbCtx.Quotes.ForGuild(guildId);

            var quoteNum = await quotesQuery.CountAsync();
            if (quoteNum == 0)
                return new Error<string>(StandardError.NoResults);

            var quote = await quotesQuery.Skip(Utils.Faker.Random.Int(0, quoteNum - 1)).FirstAsync();
            return new Success<DbQuote>(quote);
        }
        #endregion

        #region Commands - Config
        /// <summary>
        /// Configures quotes in a guild
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="enabled">Enabled?</param>
        /// <returns>GuildConfig on success / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigureQuotesAsync(IGuild guild, bool enabled)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);
            dbGuild.QuotesOn = enabled;

            _logger.LogDebug("Setting quotes to {enabled} in guild {guild}", enabled, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting quotes to {enabled} in guild {guild}", enabled, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Set quotes to {enabled} in guild {guild}", enabled, guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }

        /// <summary>
        /// Wipes quotes from a quild via search
        /// </summary>
        /// <param name="guild">Id of Guild</param>
        /// <param name="authorId">Id of Author</param>
        /// <param name="creatorId">Id of Creator</param>
        /// <param name="content">Content of Quote</param>
        /// <param name="offset">Search offset</param>
        /// <param name="limit">Search limit</param>
        /// <returns>Array of wiped quotes / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbQuote[]>, Error<string>, Error<Exception>>> WipeQuotesFromGuildAsync(IGuild guild, ulong? authorId, ulong? creatorId, string? content, int offset, int limit)
        {
            using var dbCtx = _dbService.GetContext();
            var quotes = await GetQuotesAsync(guild.Id, authorId: authorId, creatorId: creatorId, content: content, offset: offset, limit: limit);
            if (quotes.Length == 0)
                return new Error<string>(StandardError.NoResults);

            _logger.LogDebug("Wiping {quotes} from guild {guild}", quotes.Length, guild.Log());
            dbCtx.Quotes.RemoveRange(quotes);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed wiping {quotes} from guild {guild}", quotes.Length, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogDebug("Wiped {quotes} from guild {guild}", quotes.Length, guild.Log());
            return new Success<DbQuote[]>(quotes);
        }
        #endregion

        #region Utility 
        /// <summary>
        /// Gets quotes with a filter
        /// </summary>
        /// <param name="guild">Id of guild</param>
        /// <param name="authorId">Id of author</param>
        /// <param name="creatorId">Id of creator</param>
        /// <param name="quoteId">Id of quote</param>
        /// <param name="content">Content of quote</param>
        /// <param name="offset">Search offset</param>
        /// <param name="limit">Search limit</param>
        /// <returns>Search result</returns>
        private async Task<DbQuote[]> GetQuotesAsync(ulong guild, ulong? authorId = null, ulong? creatorId = null, ulong? quoteId = null, string? content = null, int offset = 0, int limit = 0)
        {
            if (authorId is null && creatorId is null && quoteId is null && content is null && offset != 0)
                return [];

            using var dbCtx = _dbService.GetContext();
            IQueryable<DbQuote> dbQuery = dbCtx.Quotes;
            if (guild != 0)
                dbQuery = dbQuery.ForGuild(guild);

            if (quoteId is not null)
                dbQuery = dbQuery.Where(x => x.QuoteId == quoteId);
            else
            {
                if (authorId is not null)
                    dbQuery = dbQuery.Where(x => x.AuthorId == authorId);
                if (creatorId is not null)
                    dbQuery = dbQuery.Where(x => x.CreatorId == creatorId);
                if (content is not null)
                    dbQuery = dbQuery.Where(x => EF.Functions.Like(x.Text, $"%{content}%"));
                if (offset > 0)
                    dbQuery = dbQuery.Skip(offset);
            }

            if (limit > 0)
                dbQuery = dbQuery.Take(limit);

            return await dbQuery.ToArrayAsync();
        }
        #endregion
    }
}
