using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Quotes
{
    [Module("quotes"), Group("cfg-quotes", "[MANAGE MESSAGES ONLY] Quotes config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageMessages), RequireUserPermission(GuildPermission.ManageMessages)]
    public sealed class QuoteConfigCommands : SolarisInteractionModuleBase
    {
        private readonly QuoteService _quoteService;

        internal QuoteConfigCommands(QuoteService quoteService)
        {
            _quoteService = quoteService;
        }

        [SlashCommand("config", "Enable quotes")]
        public async Task EnableQuotesAsync
        (
            [Summary(description: "Is feature enabled?")] bool enabled
        )
        {
            var res = await _quoteService.ConfigureQuotesAsync(Context.Guild, enabled);
            await res.Match(
                success => Interaction.ReplyAsync($"Quotes are currently **{(enabled ? "enabled" : "disabled")}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
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

            var res = await _quoteService.WipeQuotesFromGuildAsync(Context.Guild, authorId: authorIdParsed, creatorId: creatorIdParsed, content: content, offset: offset, limit: limit);
            await res.Match(
                success => Interaction.ReplyAsync($"Wiped **{success.Value.Length}** quotes from database"),
                none => Interaction.ReplyErrorAsync(GenericError.NoResults),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
