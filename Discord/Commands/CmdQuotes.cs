using Discord;
using Discord.Interactions;
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
                await RespondAsync(embed: Embeds.NoResult);
                return;
            }

            var query = guildOnly ? "guildid = " + Context.Guild.Id
                : "1 = 1";

            var quote = DbQuote.Get(query + " ORDER BY RANDOM() LIMIT 1");
            if (quote.Count == 0)
            {
                await RespondAsync(embed: Embeds.DbFailure);
                return;
            }

            await RespondWithQuoteAsync(quote[0]);
        }

        /// <summary>
        /// Responds with a quote as an embed
        /// </summary>
        /// <param name="quote">Quote to reply with</param>
        private async Task RespondWithQuoteAsync(DbQuote quote) //todo: date
        {
            var title = new StringBuilder("Quote from ");
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

            title.Append($" *(Nr.{quote.Id})*");

            await RespondAsync(embed: Embeds.Info(title.ToString(), "> " + quote.Quote));
        }
    }
}
