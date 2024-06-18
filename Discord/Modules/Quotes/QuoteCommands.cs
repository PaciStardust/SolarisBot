using Discord;
using Discord.Interactions;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Quotes
{
    [Module("quotes"), Group("quotes", "Manage Quotes"), RequireContext(ContextType.Guild)]
    public sealed class QuoteCommands : SolarisInteractionModuleBase
    {
        private readonly QuoteService _quoteService;
        internal QuoteCommands(QuoteService quoteService)
        {
            _quoteService = quoteService;
        }

        [MessageCommand("Create Quote")]
        public async Task CreateQuoteAsync(IMessage message)
        {
            var res = await _quoteService.CreateQuoteAsync(message, Context.Guild, Context.User);
            await res.Match(
                success => Interaction.ReplyAsync(GetQuoteEmbed(success.Value)),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("delete", "Delete a quote by ID")]
        public async Task DeleteQuoteAsync
        (
            [Summary(description: "ID of quote")] string quoteId
        )
        {
            if (!ulong.TryParse(quoteId, out var parsedQuoteId))
            {
                await Interaction.ReplyInvalidParameterErrorAsync("quote ID");
                return;
            }

            var res = await _quoteService.DeleteQuoteByIdAsync(Context.User, parsedQuoteId);
            await res.Match(
                success => Interaction.ReplyAsync($"Quote with ID **{parsedQuoteId}** has been deleted"),
                none => Interaction.ReplyErrorAsync(GenericError.NoResults),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("search", "Search (and view) quotes")]
        public async Task SearchAsync
        (
            [Summary(description: "[Opt] User that was quoted")] string? authorId = null,
            [Summary(description: "[Opt] User that created the quote")] string? creatorId = null,
            [Summary(description: "[Opt] Id of quote")] string? quoteId = null,
            [Summary(description: "[Opt] Text contained in quote")] string? content = null, 
            [Summary(description: "[Opt] Search offset"), MinValue(0)] int offset = 0,
            [Summary(description: "[Opt] Show first result directly?")] bool showFirst = false
        )
        {
            var authorIdParsed = Utils.ToUlongOrNull(authorId);
            if (authorId is not null && authorIdParsed is null)
            {
                await Interaction.ReplyInvalidParameterErrorAsync("author ID");
                return;
            }
            var creatorIdParsed = Utils.ToUlongOrNull(creatorId);
            if (creatorId is not null && creatorIdParsed is null)
            {
                await Interaction.ReplyInvalidParameterErrorAsync("creator ID");
                return;
            }
            var quoteIdParsed = Utils.ToUlongOrNull(quoteId);
            if (quoteId is not null && quoteIdParsed is null)
            {
                await Interaction.ReplyInvalidParameterErrorAsync("quote ID");
                return;
            }

            var quotes = await _quoteService.SearchQuotesForGuildAsync(Context.Guild.Id, authorId: authorIdParsed, creatorId: creatorIdParsed, quoteId: quoteIdParsed, content: content, offset: offset, showFirst: showFirst);
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
            [Summary(description: "[Opt] User that was quoted")] string? authorId = null,
            [Summary(description: "[Opt] Id of quote")] string? quoteId = null,
            [Summary(description: "[Opt] Text contained in quote")] string? content = null,
            [Summary(description: "[Opt] Search offset"), MinValue(0)] int offset = 0
        )
        {
            var authorIdParsed = Utils.ToUlongOrNull(authorId);
            if (authorId is not null && authorIdParsed is null)
            {
                await Interaction.ReplyInvalidParameterErrorAsync("author ID");
                return;
            }
            var quoteIdParsed = Utils.ToUlongOrNull(quoteId);
            if (quoteId is not null && quoteIdParsed is null)
            {
                await Interaction.ReplyInvalidParameterErrorAsync("quote ID");
                return;
            }

            var quotes = await _quoteService.SearchQuotesForUserAsync(Context.User.Id, authorId: authorIdParsed, quoteId: quoteIdParsed, content: content, offset: offset);
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
            var res = await _quoteService.GetRandomQuoteAsync(Context.Guild.Id);
            await res.Match(
                success => Interaction.ReplyAsync(GetQuoteEmbed(success.Value)),
                none => Interaction.ReplyErrorAsync(GenericError.NoResults)
            );
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
