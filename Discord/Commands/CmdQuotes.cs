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
        [MessageCommand("quote")] //todo: message command version
        public async Task Quote(IMessage message)
        {
            if (Context.Guild == null)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            //todo: creation limit

            var duplicate = DbQuote.Get("MessageId = " + message.Id);
            if (duplicate.Count > 0)
            {
                await RespondAsync(embed: Embeds.Info("Quotes", "You cannot quote the same message twice"));
                return;
            }
            else if (message.Content.Length > 1000)
            {
                await RespondAsync(embed: Embeds.Info("Quotes", "You cannot quote a message longer than 1000 characters"));
                return;
            }

            var quote = new DbQuote()
            {
                Quote = message.Content,
                AuthorId = message.Author.Id,
                AuthorName = message.Author.Username,
                Time = DbMain.LongToUlong(message.Timestamp.ToUnixTimeMilliseconds()),
                CreatorId = Context.User.Id,
                MessageId = message.Id
            };

            if (!quote.Create())
            {
                await RespondAsync(embed: Embeds.DbFailure);
                return;
            }

            await RespondAsync(embed: Embeds.Info($"Quote from {message.Author.Mention}", "> " + message.Content));
        }

        [SlashCommand("quote-random", "Displays a random quote")]
        public async Task QuoteRandom(bool guildOnly = true)
        {
            //todo: implement
        }
    }
}
