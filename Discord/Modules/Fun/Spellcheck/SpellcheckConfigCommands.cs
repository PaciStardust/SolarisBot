using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Fun.Spellcheck
{
    [Module("fun/spellcheck")]
    public sealed class SpellcheckConfigCommands : SolarisInteractionModuleBase
    {
        private readonly SpellcheckService _spellcheckService;
        internal SpellcheckConfigCommands(SpellcheckService spellcheckService)
        {
            _spellcheckService = spellcheckService;
        }

        [SlashCommand("cfg-spellcheck", "[MANAGE ROLES ONLY] Set a spellcheck role"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task SetSpellcheckRoleAsync
        (
            [Summary(description: "[Opt] Role to be spellchecked (none to disable)")] IRole? role = null
        )
        {
            var res = await _spellcheckService.ConfigureSpellcheckAsync(Context.Guild, role);
            await res.Match(
                success => Interaction.ReplyAsync($"Spellcheck is currently **{(role is not null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
