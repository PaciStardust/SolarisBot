﻿using Discord;
using SolarisBot.Database;
using SolarisBot.Discord.Common;

namespace SolarisBot.Discord.Modules.Roles
{
    internal static class RoleSelectHelper
    {
        internal static async Task RespondInvalidIdentifierErrorEmbedAsync(this IDiscordInteraction interaction, string identifier)
            => await interaction.ReplyErrorAsync($"Identifier **{identifier}** is invalid, identifiers can only contain letters, numbers, and spaces and must be between 2 and 20 characters long");

        internal static MessageComponent GenerateRoleGroupSelector(DbRoleGroup roleGroup)
        {
            var roles = roleGroup.RoleConfigs;

            var menuBuilder = new SelectMenuBuilder()
            {
                CustomId = $"solaris_roleselector.{roleGroup.RoleGroupId}",
                Placeholder = roleGroup.AllowOnlyOne ? "Select a role..." : "Select roles...",
                MaxValues = roleGroup.AllowOnlyOne ? 1 : roles.Count,
                Type = ComponentType.SelectMenu
            };

            foreach (var role in roles)
            {
                var desc = role.Description;
                if (string.IsNullOrWhiteSpace(desc))
                    desc = role.Identifier;
                menuBuilder.AddOption(role.Identifier, role.Identifier, desc);
            }

            return new ComponentBuilder()
                .WithSelectMenu(menuBuilder)
                .Build();
        }

        internal static DbRoleGroup? FindRoleGroupForIdentifier(IEnumerable<DbRoleGroup> roleGroups, string identifierSearch)
            => roleGroups.FirstOrDefault(x => x.Identifier.Equals(identifierSearch, StringComparison.OrdinalIgnoreCase))
                ?? roleGroups.FirstOrDefault(x => x.RoleConfigs.Any(y => y.Identifier.Equals(identifierSearch, StringComparison.OrdinalIgnoreCase)))
                ?? roleGroups.FirstOrDefault(x => x.Identifier.StartsWith(identifierSearch, StringComparison.OrdinalIgnoreCase))
                ?? roleGroups.FirstOrDefault(x => x.RoleConfigs.Any(y => y.Identifier.StartsWith(identifierSearch, StringComparison.OrdinalIgnoreCase)))
                ?? roleGroups.FirstOrDefault(x => x.Identifier.Contains(identifierSearch, StringComparison.OrdinalIgnoreCase))
                ?? roleGroups.FirstOrDefault(x => x.RoleConfigs.Any(y => y.Identifier.Contains(identifierSearch, StringComparison.OrdinalIgnoreCase)));
    }
}
