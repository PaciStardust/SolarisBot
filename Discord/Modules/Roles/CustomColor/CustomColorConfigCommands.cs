using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
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
            [Summary(description: "[Opt] Required role (none to disable)")] IRole? role = null
        )
        {
            var res = await _customColorService.ConfigCustomColorAsync(Context.Guild, role);
            await res.Match(
                success => Interaction.ReplyAsync($"Custom color creation is currently **{(role is not null ? "enabled" : "disabled")}**\n\nCreation Role: **{role?.Mention ?? "None"}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("delete-all", "Delete all custom color roles")]
        public async Task DeleteAllCustomColorRolesAsync()
        {
            var roles = Context.Guild.Roles.Where(x => x.Name.StartsWith(DiscordUtils.CustomColorRolePrefix));
            var roleCount = roles.Count();

            if (roleCount == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _logger.LogDebug("{intTag} Deleting {roleCount} custom color roles in guild {guild}", GetIntTag(), roleCount, Context.Guild.Log());
            foreach (var role in roles)
                await role.DeleteAsync();
            _logger.LogInformation("{intTag} Deleted {roleCount} custom color roles in guild {guild}", GetIntTag(), roleCount, Context.Guild.Log());
            await Interaction.ReplyAsync($"Succssfully deleted all **{roleCount}** custom color roles");
        }

        [SlashCommand("delete-ownerless", "Delete all custom color roles without owner")]
        public async Task DeleteAllMissingCustomColorRolesAsync()
        {
            var roles = Context.Guild.Roles.Where(x => x.Name.StartsWith(DiscordUtils.CustomColorRolePrefix));

            if (!roles.Any())
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var guildUsers = await Context.Guild.GetUsersAsync();
            var guildUserStringIds = guildUsers.Select(x => x.Id.ToString());
            var rolesWithoutOwner = roles.Where(x => !guildUserStringIds.Contains(DiscordUtils.GetIdFromCustomColorRoleName(x.Name)));

            var deleteRoleCount = rolesWithoutOwner.Count();

            if (deleteRoleCount == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _logger.LogDebug("{intTag} Deleting {roleCount} custom color roles without owner in guild {guild}", GetIntTag(), deleteRoleCount, Context.Guild.Log());
            foreach (var role in rolesWithoutOwner)
                await role.DeleteAsync();
            _logger.LogInformation("{intTag} Deleted {roleCount} custom color roles without owner in guild {guild}", GetIntTag(), deleteRoleCount, Context.Guild.Log());
            await Interaction.ReplyAsync($"Succssfully deleted all **{deleteRoleCount}** custom color roles without owner");
        }
    }
}
