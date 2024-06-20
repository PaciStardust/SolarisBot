using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles.RoleSelect
{
    [Module("roles/roleselect"), Group("cfg-roleselect", "[MANAGE ROLES ONLY] Role selection config commands")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
    internal class RoleSelectConfigCommands : SolarisInteractionModuleBase
    {
        private readonly RoleSelectService _rsService;
        internal RoleSelectConfigCommands(RoleSelectService rsService)
        {
            _rsService = rsService;
        }

        [SlashCommand("view-all", "View all roles and groups (Including empty ones)")]
        public async Task ViewAllRolesAsync()
        {
            var roleGroups = await _rsService.GetRoleGroupsForGuildAsync(Context.Guild.Id);
            if (roleGroups.Length == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var strings = roleGroups.OrderBy(x => x.Identifier)
                .Select(x =>
                {
                    var title = $"{x.Identifier} ({(x.AllowOnlyOne ? "One of" : "Multi")}{(x.RequiredRoleId == ulong.MinValue ? string.Empty : $", <@&{x.RequiredRoleId}> Only")})";
                    var rolesText = x.RoleConfigs.Count != 0
                        ? string.Join("\n", x.RoleConfigs.OrderBy(x => x.Identifier).Select(x => $"┗ {x.Identifier}(<@&{x.RoleId}>)"))
                        : "┗ (No roles assigned to group)";

                    return $"{title}\n{rolesText}";
                });

            await Interaction.ReplyAsync("List of Assignable Roles", string.Join("\n\n", strings));
        }

        [SlashCommand("group-create", "Create role group")]
        public async Task CreateRoleGroupAsync
        (
            [Summary(description: "Identifier of group"), MinLength(2), MaxLength(20)] string identifier,
            [Summary(description: "[Opt] Description of group?"), MaxLength(200)] string description = "",
            [Summary(description: "[Opt] Can only have one role from group?")] bool oneOf = true,
            [Summary(description: "[Opt] Required role?")] IRole? requiredrole = null
        )
        {
            var res = await _rsService.CreateRoleGroupAsync(Context.Guild, identifier, description, oneOf, requiredrole);
            await res.Match(
                success => Interaction.ReplyAsync($"Role group **\"{success.Value.Item2.Identifier}\"** {(success.Value.Item1 ? "creat" : "updat")}ed\n\nOne Of: **{(success.Value.Item2.AllowOnlyOne ? "Yes" : "No")}**\nDescription: **{(string.IsNullOrWhiteSpace(success.Value.Item2.Description) ? "None" : success.Value.Item2.Description)}**\nRequired: **{(success.Value.Item2.RequiredRoleId == ulong.MinValue ? "None" : $"<@&{success.Value.Item2.RequiredRoleId}>")}**"),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("group-delete", "Delete a role group")]
        public async Task DeleteRoleGroupAsync
        (
            [Summary(description: "Identifier of group"), MinLength(2), MaxLength(20)] string identifier
        )
        {
            var res = await _rsService.DeleteRoleGroupAsync(Context.Guild, identifier);
            await res.Match(
                success => Interaction.ReplyAsync($"The role group with the identifier **\"{success.Value.Identifier}\"** has been deleted"),
                none => Interaction.ReplyErrorAsync(GenericError.NoResults),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("role-register", "Register role group")]
        public async Task RegisterRoleAsync
        (
            [Summary(description: "Role to register")] IRole role,
            [Summary(description: "Group to register to"), MinLength(2), MaxLength(20)] string group,
            [Summary(description: "[Opt] Identifier of role"), MinLength(2), MaxLength(20)] string identifier = "",
            [Summary(description: "[Opt] Description of role"), MaxLength(200)] string description = ""
        )
        {
            var res = await _rsService.RegisterRoleAsync(Context.Guild, role, group, identifier, description);
            await res.Match(
                success => Interaction.ReplyAsync($"Role **\"{success.Value.Identifier}\"** registered\n\nGroup: **{success.Value.Identifier}**\nRole: **{role.Mention}**\nDescription: **{(string.IsNullOrWhiteSpace(success.Value.Description) ? "None" : success.Value.Description)}**"),
                none => Interaction.ReplyErrorAsync(GenericError.NoResults),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("role-unregister", "Unregister a role")]
        public async Task UnregisterRoleAsync
        (
            [Summary(description: "Identifier of group"), MinLength(2), MaxLength(20)] string group,
            [Summary(description: "Identifier of role"), MinLength(2), MaxLength(20)] string identifier
        )
        {
            var res = await _rsService.UnregisterRoleAsync(Context.Guild, group, identifier);
            await res.Match(
                success => Interaction.ReplyAsync($"A role with the identifier **\"{success.Value.Identifier}\"** has been unregistered"),
                none => Interaction.ReplyErrorAsync(GenericError.NoResults),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("spawn-permaselect", "Spawns a permanent role selector"), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task SpawnPermaselectAsync
        (
            [Summary(description: "Identifier of group"), MinLength(2), MaxLength(20)] string identifier
        )
        {
            var roleGroupMatch = await _rsService.GetRoleGroupForIdentifierAsync(Context.Guild.Id, identifier.Trim());
            if (roleGroupMatch is null)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var component = RoleSelectService.GenerateRoleGroupSelector(roleGroupMatch!);
            var title = $"Roles in group {roleGroupMatch!.Identifier}";
            if (roleGroupMatch.RequiredRoleId != ulong.MinValue)
                title += $" *(<@&{roleGroupMatch.RequiredRoleId}> only)*";
            await Interaction.ReplyComponentAsync(component, $"{title}:");
        }
    }
}
