using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Quotes
{
    [Module("quotes"), Group("cfg-quotes", "[MANAGE MESSAGES ONLY] Quotes config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageMessages), RequireUserPermission(GuildPermission.ManageMessages)]
    public sealed class QuoteConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<QuoteConfigCommands> _logger;
        private readonly DatabaseService _dbService;

        internal QuoteConfigCommands(ILogger<QuoteConfigCommands> logger, DatabaseService dbService)
        {
            _logger = logger;
            _dbService = dbService;
        }

        [SlashCommand("config", "Enable quotes")]
        public async Task EnableQuotesAsync
        (
            [Summary(description: "Is feature enabled?")] bool enabled
        )
        {
            using var dbCtx = _dbService.GetContext();
            var guild = await dbCtx.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.QuotesOn = enabled;

            _logger.LogDebug("{intTag} Setting quotes to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
            await dbCtx.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set quotes to {enabled} in guild {guild}", GetIntTag(), enabled, Context.Guild.Log());
            await Interaction.ReplyAsync($"Quotes are currently **{(enabled ? "enabled" : "disabled")}**");
        }

        [SlashCommand("wipe", "Wipe quotes from guild, make sure to search")]
        public async Task WipeQuotesAsync
        (
            [Summary(description: "[Opt] User that was quoted")] string? authorId = null,
            [Summary(description: "[Opt] User that created the quote")] string? creatorId = null,
            [Summary(description: "[Opt] Text contained in quote")] string? content = null,
            [Summary(description: "[Opt] Search offset"), MinValue(0)] int offset = 0,
            [Summary(description: "[Opt] Search limit"), MinValue(0)] int limit = 0
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

            using var dbCtx = _dbService.GetContext();
            var quotes = await dbCtx.GetQuotesAsync(Context.Guild.Id, authorId: authorIdParsed, creatorId: creatorIdParsed, content: content, offset: offset, limit: limit);
            if (quotes.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _logger.LogDebug("{intTag} Wiping {quotes} from guild {guild}", GetIntTag(), quotes.Length, Context.Guild.Log());
            dbCtx.Quotes.RemoveRange(quotes);
            await dbCtx.SaveChangesAsync();
            _logger.LogDebug("{intTag} Wiped {quotes} from guild {guild}", GetIntTag(), quotes.Length, Context.Guild.Log());
            await Interaction.ReplyAsync($"Wiped **{quotes.Length}** quotes from database");
        }
    }
}
