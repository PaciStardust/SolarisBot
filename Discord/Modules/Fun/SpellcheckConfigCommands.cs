using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Fun
{
    [Module("fun/spellcheck")]
    public sealed class SpellcheckConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<SpellcheckConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal SpellcheckConfigCommands(ILogger<SpellcheckConfigCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("cfg-spellcheck", "[MANAGE ROLES ONLY] Set a spellcheck role"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task SetSpellcheckRoleAsync
        (
            [Summary(description: "[Opt] Role to be spellchecked (none to disable)")] IRole? role = null
        )
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);
            guild.SpellcheckRoleId = role?.Id ?? 0;

            _logger.LogDebug("{intTag} Setting spellcheck-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set spellcheck-role to role {role} for guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await Interaction.ReplyAsync($"Spellcheck is currently **{(role is not null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**");
        }
    }
}
