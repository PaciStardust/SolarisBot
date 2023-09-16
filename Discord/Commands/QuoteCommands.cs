﻿using Discord.Interactions;
using Discord;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using Microsoft.EntityFrameworkCore;

namespace SolarisBot.Discord.Commands //todo: deletion on server leave, logging, random, setup, filtered search
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
        protected override ILogger? GetLogger() => _logger;

        [MessageCommand("Create Quote")]
        public async Task CreateQuoteAsync(IMessage message)
        {
            var msgLen = message.CleanContent.Length;
            if (msgLen > _botConfig.MaxQuoteCharacters)
            {
                await RespondErrorEmbedAsync("Message Too Long", $"Message is too long to quote, message has **{msgLen - _botConfig.MaxQuoteCharacters}** characters too many *(Max is {_botConfig.MaxQuoteCharacters})*", isEphemeral: true);
                return;
            }

            var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
            if (guild == null || !guild.QuotesOn)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden, isEphemeral: true);
                return;
            }

            //Check for duplicates
            if (guild.Quotes.Where(x => x.MessageId == message.Id || (x.AuthorId == message.Author.Id && x.Text == message.CleanContent && x.GId == Context.Guild.Id)).Any())
            {
                await RespondErrorEmbedAsync("Duplicate Quote", "Message has already been quoted", isEphemeral: true);
                return;
            }

            //Check if user has available slots
            if (guild.Quotes.Where(x => x.CreatorId == Context.User.Id).Count() >= _botConfig.MaxQuotesPerUser)
            {
                await RespondErrorEmbedAsync("Too Many Quotes", $"You already have **{_botConfig.MaxQuotesPerUser}** Quotes on this server, please delete some to create more", isEphemeral: true);
                return;
            }

            var dbQuote = new DbQuote()
            {
                AuthorId = message.Author.Id,
                ChannelId = message.Channel.Id,
                CreatorId = Context.User.Id,
                GId = Context.Guild.Id,
                MessageId = message.Id,
                Text = message.CleanContent,
                Time = Utils.GetCurrentUnix()
            };

            _dbContext.Quotes.Add(dbQuote);
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError, isEphemeral: true);
                return;
            }
            await RespondEmbedAsync(GetQuoteEmbed(dbQuote)); //todo: does this contain id after Add?
            return;
        }

        [SlashCommand("delete", "Delete a quote by ID")]
        public async Task DeleteQuoteAsync(ulong id)
        {
            bool isAdmin = Context.User is IGuildUser user && user.GuildPermissions.ManageMessages;

            var dbQuote = _dbContext.Quotes.Where(x => x.QId == id && (x.AuthorId == Context.User.Id || x.CreatorId == Context.User.Id || (isAdmin && Context.Guild.Id == x.GId))).FirstOrDefault();
            if (dbQuote == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            _dbContext.Quotes.Remove(dbQuote);
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError, isEphemeral: true);
                return;
            }
            await RespondEmbedAsync("Quote Deleted", $"Quote with ID **{id}** has been deleted");
        }

        private async Task<DbQuote[]> GetQuotes(IUser? author = null, IUser? creator = null, ulong? id = null, string? content = null, [MinValue(0)] int offset = 0, bool firstOnly = true, bool all = false)
        {
            if (author == null && creator == null && id == null && content == null && offset != 0)
                return Array.Empty<DbQuote>();

            IQueryable<DbQuote> dbQuery = _dbContext.Quotes;
            if (!all)
                dbQuery = dbQuery.Where(x => x.GId == Context.Guild.Id);

            if (id != null)
                dbQuery = dbQuery.Where(x => x.QId == id);
            else
            {
                if (author != null)
                    dbQuery = dbQuery.Where(x => x.AuthorId == author.Id);
                if (creator != null)
                    dbQuery = dbQuery.Where(x => x.CreatorId == creator.Id);
                if (content != null)
                    dbQuery = dbQuery.Where(x => x.Text.Contains(content, StringComparison.OrdinalIgnoreCase));
                if (offset > 0)
                    dbQuery = dbQuery.Skip(offset);
            }

            return await dbQuery.Take(firstOnly ? 1 : 10).ToArrayAsync();
        }

        private static Embed GetQuoteEmbed(DbQuote dbQuote)
            => DiscordUtils.Embed($"Quote Nr.{dbQuote.QId}", $"\"{dbQuote.Text}\" - <@{dbQuote.AuthorId}>\n\n*Created by <@{dbQuote.CreatorId}> at <t:{dbQuote.Time}:f>\n[Link to message](https://discord.com/channels/{dbQuote.GId}/{dbQuote.ChannelId}/{dbQuote.MessageId})*");
    }
}
