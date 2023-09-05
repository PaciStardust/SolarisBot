using Discord;
using Discord.Interactions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Commands
{
    [Group("config", "[ADMIN ONLY] Configure Solaris"), RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.Administrator)]
    public sealed class ConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<ConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal ConfigCommands(ILogger<ConfigCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }
        protected override ILogger? GetLogger() => _logger;

        #region Roles
        [SlashCommand("roles-list", "List all roles and groups")]
        public async Task ListRolesAsync()
        {
            var roleGroups = _dbContext.RoleGroups.Where(x => x.GId == Context.Guild.Id);

            if (!roleGroups.Any())
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                return;
            }

            var strings = (await roleGroups.ToArrayAsync()).OrderBy(x => x.Identifier)
                .Select(x =>
                {
                    var title = $"{x.Identifier} ({(x.AllowOnlyOne ? "One of" : "Multi")}{(x.RequiredRoleId == 0 ? string.Empty : $", <@&{x.RequiredRoleId}> Only")})";
                    var rolesText = x.Roles.Any()
                        ? string.Join("\n", x.Roles.OrderBy(x => x.Identifier).Select(x => $"┗ {x.Identifier}(<@&{x.RId}>)"))
                        : "┗ (No roles assigned to group)";

                    return $"{title}\n{rolesText}";
                });

            await RespondEmbedAsync("List of Assignable Roles", string.Join("\n\n", strings));
        }

        [SlashCommand("roles-create-group", "Create a role group (Group identifiers can only be made of letters, numbers, and spaces)")]
        public async Task CreateRoleGroupAsync([MinLength(2), MaxLength(20)] string identifier, [MinLength(2), MaxLength(200)] string description = "", bool oneof = true, IRole? requiredRole = null)
        {
            var identifierClean = identifier.Trim();
            var descriptionClean = description.Trim();
            if (!DiscordUtils.IsIdentifierValid(identifierClean) || descriptionClean.Length > 200)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput);
                return;
            }

            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            var lowerName = identifierClean.ToLower();
            var roleGroup = guild.RoleGroups.FirstOrDefault(x => x.Identifier.ToLower() == lowerName)
                ?? new() { GId = Context.Guild.Id, Identifier = identifierClean };

            var logVerb = roleGroup.RgId == 0 ? "Creat" : "Updat";
            roleGroup.AllowOnlyOne = oneof;
            roleGroup.Description = descriptionClean;
            roleGroup.RequiredRoleId = requiredRole?.Id ?? 0;

            _dbContext.RoleGroups.Update(roleGroup);

            _logger.LogInformation("{verb}ing role group {roleGroup} for guild {guild}", logVerb, roleGroup, Context.Guild.GetLogInfo());
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("{verb}ed role group {roleGroup} for guild {guild}", logVerb, roleGroup, Context.Guild.GetLogInfo());
            await RespondEmbedAsync($"Role Group {logVerb}ed", $"Identifier: {roleGroup.Identifier}\nOne Of: {(roleGroup.AllowOnlyOne ? "Yes" : "No")}\nDescription: {(string.IsNullOrWhiteSpace(roleGroup.Description) ? "None" : roleGroup.Description)}\nRequired: {(roleGroup.RequiredRoleId == 0 ? "None" : $"<@&{roleGroup.RequiredRoleId}>")}");
        }

        [SlashCommand("roles-delete-group", "Delete a role group")]
        public async Task DeleteRoleGroupAsync(string identifier)
        {
            var identifierClean = identifier.Trim();
            if (!DiscordUtils.IsIdentifierValid(identifierClean))
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput);
                return;
            }

            var lowerName = identifierClean.ToLower();
            var match = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GId == Context.Guild.Id && x.Identifier.ToLower() == lowerName);
            if (match == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                return;
            }

            _dbContext.RoleGroups.Remove(match);

            _logger.LogInformation("Deleting role group {roleGroup} from guild {guild}", match, Context.Guild.GetLogInfo());
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Deleted role group {roleGroup} from guild {guild}", match, Context.Guild.GetLogInfo());
            await RespondEmbedAsync("Role Group Deleted", $"The role group with the identifier \"{identifierClean}\" has been deleted");
        }

        [SlashCommand("roles-register-role", "Register a role to a group (Identifiers can only be made of letters, numbers, and spaces)")]
        public async Task RegisterRoleAsync(IRole role, [MinLength(2), MaxLength(20)] string identifier, string group, string description = "")
        {
            var descriptionClean = description.Trim();
            var identifierNameClean = identifier.Trim();
            var groupNameCleanLower = group.Trim().ToLower();

            if (!DiscordUtils.IsIdentifierValid(identifierNameClean) || !DiscordUtils.IsIdentifierValid(groupNameCleanLower) || descriptionClean.Length > 200)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput);
                return;
            }

            if (await _dbContext.Roles.FirstOrDefaultAsync(x => x.RId == role.Id) != null)
            {
                await RespondErrorEmbedAsync("Already Registered", "Role is already registered");
                return;
            }

            var roleGroup = await _dbContext.RoleGroups.FirstOrDefaultAsync(x => x.GId == Context.Guild.Id && x.Identifier.ToLower() == groupNameCleanLower);
            if (roleGroup == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                return;
            }

            var lowerIdentifierName = identifierNameClean.ToLower();
            if (roleGroup.Roles.FirstOrDefault(x => x.Identifier.ToLower() == lowerIdentifierName) != null)
            {
                await RespondErrorEmbedAsync("Already Registered", "A Role with that identifier is already registered");
                return;
            }

            var dbRole = new DbRole()
            {
                Identifier = identifierNameClean,
                RId = role.Id,
                RgId = roleGroup.RgId,
                Description = descriptionClean
            };

            _dbContext.Roles.Add(dbRole);

            _logger.LogInformation("Registering role {role} to group {roleGroup} in guild {guild}", dbRole, roleGroup, Context.Guild.GetLogInfo());
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Registered role {role} to group {roleGroup} in guild {guild}", dbRole, roleGroup, Context.Guild.GetLogInfo());
            await RespondEmbedAsync("Role Registered", $"Group: {roleGroup.Identifier}\nIdentifier: {dbRole.Identifier}\nDescription: {(string.IsNullOrWhiteSpace(dbRole.Description) ? "None" : dbRole.Description)}");
        }

        [SlashCommand("roles-unregister-role", "Unregister a role")]
        public async Task UnregisterRoleAsync(string identifier)
        {
            var identifierClean = identifier.Trim().ToLower();

            if (!DiscordUtils.IsIdentifierValid(identifierClean))
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput);
                return;
            }

            var role = await _dbContext.Roles.FirstOrDefaultAsync(x => x.Identifier.ToLower() == identifierClean);
            if (role == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                return;
            }

            _dbContext.Roles.Remove(role);

            _logger.LogInformation("Unregistering role {role} from groups", role);
            if (await _dbContext.TrySaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Unregistered role {role} from groups", role);
            await RespondEmbedAsync("Role Unegistered", $"A role with the identifier \"{identifierClean}\" has been unregistered");
        }
        #endregion

        #region Other
        [SlashCommand("vouching", "Set up vouching (Not setting either disables vouching)")]
        public async Task ConfigVouchingAsync(IRole? permission, IRole? vouch)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.VouchPermissionRoleId = permission?.Id ?? 0;
            guild.VouchRoleId = vouch?.Id ?? 0;

            _logger.LogInformation("Setting vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", permission?.GetLogInfo() ?? "0", vouch?.GetLogInfo() ?? "0", Context.Guild.GetLogInfo());
            if (await _dbContext.SaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Set vouching to permission={vouchPermission}, vouch={vouch} in guild {guild}", permission?.GetLogInfo() ?? "0", vouch?.GetLogInfo() ?? "0", Context.Guild.GetLogInfo());
            await RespondEmbedAsync("Vouching Configured", $"Vouching is currently **{(permission != null && vouch != null ? "enabled" : "disabled")}**\n\nPermission: {permission?.Mention ?? "None"}\nVouch: {vouch?.Mention ?? "None"}");
        }

        [SlashCommand("magic", "Set up magic role (Not setting role disables it)")]
        public async Task ConfigureMagicAsync(IRole? role = null, ulong timeoutsecs = 1800, bool renaming = false)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.MagicRoleId = role?.Id ?? 0;
            guild.MagicRoleNextUse = 0;
            guild.MagicRoleTimeout = timeoutsecs >= 0 ? timeoutsecs : 0;
            guild.MagicRoleRenameOn = renaming;

            _logger.LogInformation("Setting magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", role?.GetLogInfo() ?? "0", guild.MagicRoleTimeout, guild.MagicRoleRenameOn, Context.Guild.GetLogInfo());
            if (await _dbContext.SaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Set magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", role?.GetLogInfo() ?? "0", guild.MagicRoleTimeout, guild.MagicRoleRenameOn, Context.Guild.GetLogInfo());
            await RespondEmbedAsync("Magic Configured", $"Magic is currently **{(role != null ? "enabled" : "disabled")}**\n\nRole: {role?.Mention ?? "None"}\nTimeout: {guild.MagicRoleTimeout}\nRenaming: {guild.MagicRoleRenameOn}");
        }

        [SlashCommand("custom-color", "Set up custom color creation (Not setting disabled it)")]
        public async Task ConfigureCustomColorAsync(IRole? creationrole = null)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.CustomColorPermissionRoleId = creationrole?.Id ?? 0;

            _logger.LogInformation("Setting custom colors to role={role} in guild {guild}", creationrole?.GetLogInfo() ?? "0", Context.Guild.GetLogInfo());
            if (await _dbContext.SaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Set custom colors to role={role} in guild {guild}", creationrole?.GetLogInfo() ?? "0", Context.Guild.GetLogInfo());
            await RespondEmbedAsync("Custom Colors Configured", $"Custom color creation is currently **{(creationrole != null ? "enabled" : "disabled")}**\n\nCreation Role: {creationrole?.Mention ?? "None"}");
        }
        #endregion
    }
}
