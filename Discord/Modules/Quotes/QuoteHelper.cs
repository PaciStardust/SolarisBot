using Discord;
using Microsoft.EntityFrameworkCore;
using SolarisBot.Database;

namespace SolarisBot.Discord.Modules.Quotes
{
    internal static class QuoteHelper
    {
        internal static async Task<DbQuote[]> GetQuotesAsync(this DatabaseContext dbCtx, ulong guild, IUser? author = null, IUser? creator = null, ulong? id = null, string? content = null, int offset = 0, int limit = 0)
        {
            if (author is null && creator is null && id is null && content is null && offset != 0)
                return Array.Empty<DbQuote>();

            IQueryable<DbQuote> dbQuery = dbCtx.Quotes;
            if (guild != 0)
                dbQuery = dbQuery.ForGuild(guild);

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
    }
}
