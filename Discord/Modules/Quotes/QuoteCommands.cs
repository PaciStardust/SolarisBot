using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Quotes
{
    [Module("quotes"), Group("quotes", "Manage Quotes"), RequireContext(ContextType.Guild)]
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

        [MessageCommand("Create Quote")]
        public async Task CreateQuoteAsync(IMessage message)
        {
            var msgLen = message.CleanContent.Length;
            if (msgLen > _botConfig.MaxQuoteCharacters)
            {
                await Interaction.ReplyErrorAsync($"Message is too long to quote, message has **{msgLen - _botConfig.MaxQuoteCharacters}** characters too many *(Max is {_botConfig.MaxQuoteCharacters})*");
                return;
            }

            var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id, x => x.Include(y => y.Quotes));
            if (guild is null || !guild.QuotesOn)
            {
                await Interaction.ReplyErrorAsync("Quotes are not enabled in this guild");
                return;
            }

            //Check for duplicates
            if (guild.Quotes.Any(x => x.MessageId == message.Id || x.AuthorId == message.Author.Id && x.Text == message.CleanContent && x.GuildId == Context.Guild.Id))
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
                CreatedAt = Utils.GetCurrentUnix()
            };

            _logger.LogDebug("{intTag} Adding quote {quote} by user {user} to guild {guild}", GetIntTag(), dbQuote, Context.User.Log(), Context.Guild.Log());
            guild.Quotes.Add(dbQuote);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Added quote {quote} by user {user} to guild {guild}", GetIntTag(), dbQuote, Context.User.Log(), Context.Guild.Log());
            await Interaction.ReplyAsync(GetQuoteEmbed(dbQuote));
            return;
        }

        [SlashCommand("delete", "Delete a quote by ID")]
        public async Task DeleteQuoteAsync
        (
            [Summary(description: "ID of quote")] ulong id
        )
        {
            var user = GetGuildUser(Context.User);
            bool isAdmin = user?.GuildPermissions.ManageMessages ?? false;

            var dbQuote = await _dbContext.Quotes.FirstOrDefaultAsync(x => x.QuoteId == id && (x.AuthorId == Context.User.Id || x.CreatorId == Context.User.Id || isAdmin && Context.Guild.Id == x.GuildId));
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
        public async Task SearchAsync
        (
            [Summary(description: "[Opt] User that was quoted")] IUser? author = null,
            [Summary(description: "[Opt] User that created the quote")] IUser? creator = null,
            [Summary(description: "[Opt] Id of quote")] ulong? id = null,
            [Summary(description: "[Opt] Text contained in quote")] string? content = null, 
            [Summary(description: "[Opt] Search offset"), MinValue(0)] int offset = 0,
            [Summary(description: "[Opt] Show first result directly?")] bool showFirst = false
        )
        {
            var quotes = await _dbContext.GetQuotesAsync(Context.Guild.Id, author: author, creator: creator, id: id, content: content, offset: offset, limit: showFirst ? 1 : 10);
            if (quotes.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }
            else if (showFirst)
            {
                await Interaction.ReplyAsync(GetQuoteEmbed(quotes[0]));
                return;
            }
            await Interaction.ReplyAsync("Quote Search Results", GenerateQuotesList(quotes));
        }

        [SlashCommand("search-self", "Search through own quotes, not limited by guild")]
        public async Task SearchSelfAsync
        (
            [Summary(description: "[Opt] User that was quoted")] IUser? author = null,
            [Summary(description: "[Opt] Id of quote")] ulong? id = null,
            [Summary(description: "[Opt] Text contained in quote")] string? content = null,
            [Summary(description: "[Opt] Search offset"), MinValue(0)] int offset = 0
        )
        {
            var quotes = await _dbContext.GetQuotesAsync(0, author: author, id: id, content: content, offset: offset);
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
            var quotesQuery = _dbContext.Quotes.ForGuild(Context.Guild.Id);

            var quoteNum = await quotesQuery.CountAsync();
            if (quoteNum == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var quote = await quotesQuery.Skip(Utils.Faker.Random.Int(0, quoteNum - 1)).FirstAsync();
            await Interaction.ReplyAsync(GetQuoteEmbed(quote));
        }

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
            => EmbedFactory.Default($"Quote #{dbQuote.QuoteId}", $"\"{dbQuote.Text}\" - <@{dbQuote.AuthorId}>\n\n*Created by <@{dbQuote.CreatorId}> at <t:{dbQuote.CreatedAt}:f>\n[Link to message](https://discord.com/channels/{dbQuote.GuildId}/{dbQuote.ChannelId}/{dbQuote.MessageId})*");
        #endregion
    }
}
