using Discord;
using SolarisBot.Discord.Common;

namespace SolarisBot.Discord.Modules.Roles
{
    internal static class RoleSelectHelper //todo: [REFACTOR] change how helpers work?
    {
        internal static async Task RespondInvalidIdentifierErrorEmbedAsync(this IDiscordInteraction interaction, string identifier)
            => await interaction.ReplyErrorAsync($"Identifier **{identifier}** is invalid, identifiers can only contain letters, numbers, and spaces and must be between 2 and 20 characters long");
    }
}
