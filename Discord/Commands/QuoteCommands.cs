using Discord.Interactions;
using Discord;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using Microsoft.EntityFrameworkCore;
using SolarisBot.Discord.Common;

namespace SolarisBot.Discord.Commands
{
    [Group("quotes", "Manage Quotes"), RequireContext(ContextType.Guild)]
    public sealed class QuoteCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<QuoteCommands> _logger;
        private readonly DatabaseContext _dbContext;
        private readonly BotConfig _botConfig;
        internal QuoteCommands(ILogger<QuoteCommands> logger, DatabaseContext dbctx, BotConfig botConfig)
        {
            _dbContext = dbctx;
            _logger = logger;
            _botConfig = botConfig;
        }

        #region Admin
        [SlashCommand("config", "[MANAGE GUILD ONLY] Enable quotes"), DefaultMemberPermissions(GuildPermission.ManageGuild), RequireUserPermission(GuildPermission.ManageGuild)]
        public async Task EnableQuotesAsync(bool enabled)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.QuotesOn = enabled;

            _logger.LogDebug("{intTag} Setting quotes to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set quotes to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
            await Interaction.ReplyAsync($"Quotes are currently **{(enabled ? "enabled" : "disabled")}**");
        }

        [SlashCommand("wipe", "[MANAGE MESSAGES ONLY] Wipe quotes from guild, make sure to search"), DefaultMemberPermissions(GuildPermission.ManageMessages), RequireUserPermission(ChannelPermission.ManageMessages)]
        public async Task WipeQuotesAsync(IUser? author = null, IUser? creator = null, string? content = null, [MinValue(0)] int offset = 0, [MinValue(0)] int limit = 0)
        {
            var quotes = await GetQuotesAsync(author: author, creator: creator, content: content, offset: offset, limit: limit);
            if (quotes.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _logger.LogDebug("{intTag} Wiping {quotes} from guild {guild}", GetIntTag(), quotes.Length, Context.Guild.Log());
            _dbContext.Quotes.RemoveRange(quotes);
            await _dbContext.SaveChangesAsync();
            _logger.LogDebug("{intTag} Wiped {quotes} from guild {guild}", GetIntTag(), quotes.Length, Context.Guild.Log());
            await Interaction.ReplyAsync($"Wiped **{quotes.Length}** quotes from database");
        }
        #endregion

        #region Users
        [MessageCommand("Create Quote")]
        public async Task CreateQuoteAsync(IMessage message)
        {
            var msgLen = message.CleanContent.Length;
            if (msgLen > _botConfig.MaxQuoteCharacters)
            {
                await Interaction.ReplyErrorAsync($"Message is too long to quote, message has **{msgLen - _botConfig.MaxQuoteCharacters}** characters too many *(Max is {_botConfig.MaxQuoteCharacters})*");
                return;
            }

            var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
            if (guild is null || !guild.QuotesOn)
            {
                await Interaction.ReplyErrorAsync(GenericError.Forbidden);
                return;
            }

            //Check for duplicates
            if (guild.Quotes.Any(x => x.MessageId == message.Id || (x.AuthorId == message.Author.Id && x.Text == message.CleanContent && x.GuildId == Context.Guild.Id)))
            {
                await Interaction.ReplyErrorAsync("Message has already been quoted");
                return;
            }

            //Check if user has available slots
            if (guild.Quotes.Count(x => x.CreatorId == Context.User.Id) >= _botConfig.MaxQuotesPerUser)
            {
                await Interaction.ReplyErrorAsync($"You already have **{_botConfig.MaxQuotesPerUser}** Quotes on this server, please delete some to create more");
                return;
            }

            var dbQuote = new DbQuote()
            {
                AuthorId = message.Author.Id,
                ChannelId = message.Channel.Id,
                CreatorId = Context.User.Id,
                GuildId = Context.Guild.Id,
                MessageId = message.Id,
                Text = message.CleanContent,
                Time = Utils.GetCurrentUnix()
            };

            _logger.LogDebug("{intTag} Adding quote {quote} by user {user} to guild {guild}", GetIntTag(), dbQuote, Context.User.Log(), Context.Guild.Log());
            _dbContext.Quotes.Add(dbQuote);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Added quote {quote} by user {user} to guild {guild}", GetIntTag(), dbQuote, Context.User.Log(), Context.Guild.Log());
            await Interaction.ReplyAsync(GetQuoteEmbed(dbQuote));
            return;
        }

        [SlashCommand("delete", "Delete a quote by ID")]
        public async Task DeleteQuoteAsync(ulong id)
        {
            var user = GetGuildUser()!;
            bool isAdmin = user?.GuildPermissions.ManageMessages ?? false;

            var dbQuote = await _dbContext.Quotes.FirstOrDefaultAsync(x => x.QuoteId == id && (x.AuthorId == Context.User.Id || x.CreatorId == Context.User.Id || (isAdmin && Context.Guild.Id == x.GuildId)));
            if (dbQuote is null)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _logger.LogDebug("{intTag} Removing quote {quote} from guild {guild}", GetIntTag(), dbQuote, Context.Guild.Id);
            _dbContext.Quotes.Remove(dbQuote);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Removed quote {quote} from guild {guild}", GetIntTag(), dbQuote, Context.Guild.Id);
            await Interaction.ReplyAsync($"Quote with ID **{id}** has been deleted");
        }

        [SlashCommand("search", "Search (and view) quotes")]
        public async Task SearchAsync(IUser? author = null, IUser? creator = null, ulong? id = null, string? content = null, [MinValue(0)] int offset = 0, bool showfirst = false)
        {
            var quotes = await GetQuotesAsync(author: author, creator: creator, id: id, content: content, offset: offset, limit: showfirst ? 1 : 10);
            if (quotes.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }
            else if (showfirst)
            {
                await Interaction.ReplyAsync(GetQuoteEmbed(quotes[0]));
                return;
            }
            await Interaction.ReplyAsync("Quote Search Results", GenerateQuotesList(quotes));
        }

        [SlashCommand("search-self", "Search through own quotes, not limited by guild")]
        public async Task SearchSelfAsync(IUser? author = null, ulong? id = null, string? content = null, [MinValue(0)] int offset = 0)
        {
            var quotes = await GetQuotesAsync(author: author, id: id, content: content, offset: offset, all: true);
            if (quotes.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }
            await Interaction.ReplyAsync("Quote Search Results", GenerateQuotesList(quotes));
        }

        [SlashCommand("random", "Picks a random quote")]
        public async Task RandomQuoteAsync()
        {
            var quotesQuery = _dbContext.Quotes.Where(x => x.GuildId == Context.Guild.Id);

            var quoteNum = await quotesQuery.CountAsync();
            if (quoteNum == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var quote = await quotesQuery.Skip(Utils.Faker.Random.Int(0, quoteNum-1)).FirstAsync();
            await Interaction.ReplyAsync(GetQuoteEmbed(quote));
        }

        private async Task<DbQuote[]> GetQuotesAsync(IUser? author = null, IUser? creator = null, ulong? id = null, string? content = null, int offset = 0, int limit = 0, bool all = false)
        {
            if (author is null && creator is null && id is null && content is null && offset != 0)
                return Array.Empty<DbQuote>();

            IQueryable<DbQuote> dbQuery = _dbContext.Quotes;
            if (!all)
                dbQuery = dbQuery.Where(x => x.GuildId == Context.Guild.Id);

            if (id is not null)
                dbQuery = dbQuery.Where(x => x.QuoteId == id);
            else
            {
                if (author is not null)
                    dbQuery = dbQuery.Where(x => x.AuthorId == author.Id);
                if (creator is not null)
                    dbQuery = dbQuery.Where(x => x.CreatorId == creator.Id);
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

        #region Utils
        /// <summary>
        /// Generates a list of quotes for discord embeds
        /// </summary>
        private static string GenerateQuotesList(DbQuote[] quotes)
            => string.Join("\n\n", quotes.Select(x => x.ToString()));

        /// <summary>
        /// Generates a discord embed for a quote
        /// </summary>
        private static Embed GetQuoteEmbed(DbQuote dbQuote)
            => EmbedFactory.Default($"Quote #{dbQuote.QuoteId}", $"\"{dbQuote.Text}\" - <@{dbQuote.AuthorId}>\n\n*Created by <@{dbQuote.CreatorId}> at <t:{dbQuote.Time}:f>\n[Link to message](https://discord.com/channels/{dbQuote.GuildId}/{dbQuote.ChannelId}/{dbQuote.MessageId})*");
        #endregion
    }
}
