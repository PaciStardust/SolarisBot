using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Commands
{
    [Group("custom-color", "Tweak your custom color (Requires permission role)"), RequireContext(ContextType.Guild)]
    public sealed class CustomColorCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<CustomColorCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal CustomColorCommands(ILogger<CustomColorCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }
        protected override ILogger? GetLogger() => _logger;

        #region Create
        [SlashCommand("config", "[MANAGE ROLES ONLY] Set up custom color creation (Not setting disabled it)"), DefaultMemberPermissions(GuildPermission.ManageRoles)]
        public async Task ConfigureCustomColorAsync(IRole? creationrole = null)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.CustomColorPermissionRoleId = creationrole?.Id ?? 0;

            _logger.LogDebug("Setting custom colors to role={role} in guild {guild}", creationrole?.Log() ?? "0", Context.Guild.Log());
            if (await _dbContext.SaveChangesAsync() == -1)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                return;
            }
            _logger.LogInformation("Set custom colors to role={role} in guild {guild}", creationrole?.Log() ?? "0", Context.Guild.Log());
            await RespondEmbedAsync("Custom Colors Configured", $"Custom color creation is currently **{(creationrole != null ? "enabled" : "disabled")}**\n\nCreation Role: **{creationrole?.Mention ?? "None"}**");
        }

        [SlashCommand("set-color-rgb", "Set your custom role color via RGB (Requires permission role)"), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task SetRoleColorByRgb(byte red, byte green, byte blue)
            => await SetRoleColorAsync(new(red, green, blue));

        private static readonly Regex _hexCodeValidator = new(@"[A-F0-9]{6}");

        [SlashCommand("set-color-hex", "Set your custom role color via Hex (Requires permission role)"), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task SetRoleColorByHex([MinLength(6), MaxLength(6)] string hex)
        {
            var upperHex = hex.ToUpper();
            if (!_hexCodeValidator.IsMatch(upperHex) || !uint.TryParse(upperHex, System.Globalization.NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var colorNumber))
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.InvalidInput, isEphemeral:true);
                return;
            }
            await SetRoleColorAsync(new(colorNumber));
        }

        private async Task SetRoleColorAsync(Color color)
        {
            if (Context.User is not SocketGuildUser gUser)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden, isEphemeral: true);
                return;
            }

            var roleName = DiscordUtils.GetCustomColorRoleName(gUser);
            var role = Context.Guild.Roles.FirstOrDefault(x => x.Name == roleName);

            var requiredRole = (await _dbContext.GetGuildByIdAsync(Context.Guild.Id))?.CustomColorPermissionRoleId;
            if (role == null && (requiredRole == null || requiredRole == 0 || gUser.Roles.FirstOrDefault(x => x.Id == requiredRole) == null))
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden, isEphemeral: true);
                return;
            }

            try
            {
                if (role == null)
                {
                    _logger.LogDebug("Creating custom color role {roleName} for user {user} in guild {guild}", roleName, gUser.Log(), Context.Guild.Log());
                    role = await Context.Guild.CreateRoleAsync(roleName, color: color, isMentionable: false);
                    _logger.LogInformation("Created custom color role {role} for user {user} in guild {guild}", role.Log(), gUser.Log(), Context.Guild.Log());
                }
                else
                {
                    _logger.LogDebug("Modifying custom color role {role} for user {user} in guild {guild}", role.Log(), gUser.Log(), Context.Guild.Log());
                    await role.ModifyAsync(x => x.Color = color);
                    _logger.LogInformation("Modified custom color role {role} for user {user} in guild {guild}", role.Log(), gUser.Log(), Context.Guild.Log());
                }

                if (!gUser.Roles.Contains(role))
                {
                    _logger.LogDebug("Adding custom color role {role} to user {user} in guild {guild}", role.Log(), gUser.Log(), Context.Guild.Log());
                    await gUser.AddRoleAsync(role);
                    _logger.LogInformation("Added custom color role {role} to user {user} in guild {guild}", role.Log(), gUser.Log(), Context.Guild.Log());
                }
            }
            catch (Exception ex)
            {
                await RespondErrorEmbedAsync(ex);
                return;
            }
            await RespondEmbedAsync("Custom Color Set", $"Custom color role has been set to {role.Mention}", color, isEphemeral: true);
        }
        #endregion

        #region Delete
        [SlashCommand("delete", "Delete your custom color role"), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task DeleteCustomColorRoleAsync()
        {
            var roleName = DiscordUtils.GetCustomColorRoleName(Context.User);
            var role = Context.Guild.Roles.FirstOrDefault(x => x.Name == roleName);

            if (role == null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults, isEphemeral: true);
                return;
            }

            try
            {
                _logger.LogDebug("Deleting custom color role {role} from {guild}", role.Log(), Context.Guild.Log());
                await role.DeleteAsync();
                _logger.LogInformation("Deleted custom color role {role} from {guild}", role.Log(), Context.Guild.Log());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete custom color role {role} from {guild}", role.Log(), Context.Guild.Log());
                await RespondErrorEmbedAsync(ex, isEphemeral: true);
                return;
            }
            await RespondEmbedAsync("Role Deleted", "Deleted your custom color role", isEphemeral:true);
        }

        [SlashCommand("delete-all", "[REQUIRES MANAGE ROLES] Delete all custom color roles"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task DeleteAllCustomColorRolesAsync()
        {
            var roles = Context.Guild.Roles.Where(x => x.Name.StartsWith(DiscordUtils.CustomColorRolePrefix));
            var roleCount = roles.Count();

            if (roleCount == 0)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                return;
            }

            try
            {
                _logger.LogDebug("Deleting {roleCount} custom color roles in guild {guild}", roleCount, Context.Guild.Log());
                foreach (var role in roles)
                    await role.DeleteAsync();
                _logger.LogInformation("Deleted {roleCount} custom color roles in guild {guild}", roleCount, Context.Guild.Log());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Deleted {roleCount} custom color roles in guild {guild}", roleCount, Context.Guild.Log());
                await RespondErrorEmbedAsync(ex);
                return;
            }
            await RespondEmbedAsync("Deleted Custom Colors", $"Succssfully deleted all **{roleCount}** custom color roles");
        }
        #endregion
    }
}
