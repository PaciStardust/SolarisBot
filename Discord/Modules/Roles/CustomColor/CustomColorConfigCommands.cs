using Discord;
using Discord.Interactions;
using OneOf.Types;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles.CustomColor
{
    [Module("roles/customcolor"), Group("cfg-customcolor", "[MANAGE ROLES ONLY] Set up custom color creation")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
    public sealed class CustomColorConfigCommands : SolarisInteractionModuleBase
    {
        private readonly CustomColorService _customColorService;

        internal CustomColorConfigCommands(CustomColorService customColorService)
        {
            _customColorService = customColorService;
        }

        [SlashCommand("config", "Set up custom color creation")]
        public async Task ConfigureCustomColorAsync
        (
            [Summary(description: "[Opt] Required role (none to disable)")] IRole? role = null,
            [Summary(description: "[Opt] Indicator")] string indicator = ""
        )
        {
            var res = await _customColorService.ConfigCustomColorAsync(Context.Guild, role, indicator);
            await res.Match(
                success => Interaction.ReplyAsync($"Custom color creation is currently **{(role is not null ? "enabled" : "disabled")}**\n\nCreation Role: **{role?.Mention ?? "None"}**\nCreation Role: **{(string.IsNullOrWhiteSpace(indicator) ? "None" : indicator)}**"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("delete-all", "Delete all custom color roles")]
        public async Task DeleteAllCustomColorRolesAsync()
        {
            var res = await _customColorService.DeleteCustomColorRolesForGuildAsync(Context.Guild);
            await res.Match(
                success => Interaction.ReplyAsync($"Succssfully deleted all **{success.Value.Length}** custom color roles"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("delete-ownerless", "Delete all custom color roles without owner")]
        public async Task DeleteAllMissingCustomColorRolesAsync()
        {
            var res = await _customColorService.DeleteOwnerlessCustomColorRolesForGuildAsync(Context.Guild);
            await res.Match(
                success => Interaction.ReplyAsync($"Succssfully deleted all **{success.Value.Length}** custom color roles without owner"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
