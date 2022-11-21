using Discord;
using Discord.Interactions;
using Microsoft.Data.Sqlite;
using SolarisBot.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.Discord.Commands
{
    [Group("quotes", "Quote Related Commands")]
    public class CmdQuotes : InteractionModuleBase
    {
        [MessageCommand("quote")]
        public async Task Quote(IMessage message)
        {
            if (Context.Guild == null)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var userCreated = DbQuote.Get("CreatorId = " + Context.User.Id);
            if (userCreated.Count > Config.Command.MaxQuotesPerPerson)
            {
                await RespondAsync(embed: Embeds.Info("Quote limit reached", "You have reached your limit of 100 quotes"));
                return;
            }

            var duplicate = DbQuote.Get("MessageId = " + message.Id);
            if (duplicate.Count > 0)
            {
                await RespondAsync(embed: Embeds.Info("Duplicate quote", "You cannot quote the same message twice"));
                return;
            }
            else if (message.Content.Length > 1000)
            {
                await RespondAsync(embed: Embeds.Info("Quote too long", "You cannot quote a message longer than 1000 characters"));
                return;
            }

            var quote = new DbQuote()
            {
                Quote = message.Content,
                AuthorId = message.Author.Id,
                AuthorName = message.Author.Username,
                Time = DbMain.LongToUlong(message.Timestamp.ToUnixTimeMilliseconds()),
                CreatorId = Context.User.Id,
                MessageId = message.Id,
                GuildId = Context.Guild.Id
            };

            if (!quote.Create())
            {
                await RespondAsync(embed: Embeds.DbFailure);
                return;
            }

            await RespondAsync(embed: Embeds.Info($"Quote from {message.Author.Mention}", "> " + message.Content));
        }

        [SlashCommand("random", "Displays a random quote")]
        public async Task Random(bool guildOnly = true)
        {
            if (Context.Guild == null)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var query = guildOnly ? "guildid = " + Context.Guild.Id
                : "1 = 1";

            var quote = DbQuote.Get(query + " ORDER BY RANDOM() LIMIT 1");
            if (quote.Count == 0)
            {
                await RespondAsync(embed: Embeds.NoResult);
                return;
            }

            await RespondWithQuoteAsync(quote[0]);
        }

        [SlashCommand("id", "Displays a quote with id")]
        public async Task Id(ulong id)
        {
            if (Context.Guild == null)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var quote = DbQuote.GetOne(id);
            if (quote == null)
            {
                await RespondAsync(embed: Embeds.NoResult);
                return;
            }

            await RespondWithQuoteAsync(quote.Value);
        }

        [SlashCommand("search", "Search for a quote")]
        public async Task Search(IUser? author = null, IUser? creator = null, ulong? guild = null, string? content = null, uint offset = 0, bool direct = true)
        {
            if (Context.Guild == null)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var queryParts = new List<string>();
            var sqlParts = new List<SqliteParameter>();

            if (author != null)
                queryParts.Add("authorid = " + author.Id);
            if (creator != null)
                queryParts.Add("creatorid = " + creator.Id);
            if (guild != null)
                queryParts.Add("guildid = " + guild);
            if (content != null)
            {
                content = $"%{content.ToLower()}%";
                queryParts.Add("LOWER(quote) LIKE @QUOTE");
                sqlParts.Add(new("QUOTE", content));
            }

            var query = (queryParts.Count > 0 ? string.Join(" AND ", queryParts) : "1 = 1") + $" LIMIT {(direct ? 1 : 10)} OFFSET {offset}";

            var result = DbQuote.Get(query, sqlParts.ToArray());

            if (result.Count == 0)
            {
                await RespondAsync(embed: Embeds.NoResult);
                return;
            }
            
            if (direct)
            {
                await RespondWithQuoteAsync(result[0]);
                return;
            }

            var quoteStrings = result.Select(x => $"Nr.{x.Id} from {x.AuthorName}\n> {(x.Quote.Length > 50 ? x.Quote[..50] : x.Quote)}");
            await RespondAsync(embed: Embeds.Info("Quote search results", $"```{string.Join("\n\n", quoteStrings)}```"));
        }

        [SlashCommand("delete", "Delete a quote")]
        public async Task Delete(ulong id)
        {
            if (Context.Guild == null)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var quote = DbQuote.GetOne(id);

            if (quote == null)
            {
                await RespondAsync(embed: Embeds.NoResult);
                return;
            }

            if (quote.Value.AuthorId != Context.User.Id && quote.Value.CreatorId != Context.User.Id)
            {
                await RespondAsync(embed: Embeds.Info("Quote deletion denied", "You are not the author or creator of this quote"));
                return;
            }

            if (DbMain.Run("DELETE FROM quotes WHERE id = " + id) < 1)
            {
                await RespondAsync(embed: Embeds.DbFailure);
                return;
            }

            await RespondAsync(embed: Embeds.Info("Quote deleted", $"Quote Nr.{id} has been deleted"));
        }

        /// <summary>
        /// Responds with a quote as an embed
        /// </summary>
        /// <param name="quote">Quote to reply with</param>
        private async Task RespondWithQuoteAsync(DbQuote quote) //todo: date
        {
            var title = new StringBuilder($"Quote Nr.{quote.Id} from ");
            if (Config.Command.TagQuoteIfPossible)
            {
                var gUser = await Context.Guild.GetUserAsync(quote.AuthorId);
                if (gUser == null)
                    title.Append(quote.AuthorName);
                else
                    title.Append(gUser.Mention);
            }
            else
                title.Append(quote.AuthorName);

            await RespondAsync(embed: Embeds.Info(title.ToString(), quote.Quote));
        }
    }
}
