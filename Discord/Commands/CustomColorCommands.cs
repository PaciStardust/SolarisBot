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
        [SlashCommand("config", "[MANAGE ROLES ONLY] Set up custom color creation (Not setting disabled it)"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(ChannelPermission.ManageRoles)]
        public async Task ConfigureCustomColorAsync(IRole? creationrole = null)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.CustomColorPermissionRoleId = creationrole?.Id ?? ulong.MinValue;

            _logger.LogDebug("{intTag} Setting custom colors to role={role} in guild {guild}", GetIntTag(), creationrole?.Log() ?? "0", Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set custom colors to role={role} in guild {guild}", GetIntTag(), creationrole?.Log() ?? "0", Context.Guild.Log());
            await RespondEmbedAsync("Custom Colors Configured", $"Custom color creation is currently **{(creationrole is not null ? "enabled" : "disabled")}**\n\nCreation Role: **{creationrole?.Mention ?? "None"}**");
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
                await RespondInvalidInputErrorEmbedAsync($"Failed to convert **{upperHex}** to hex code");
                return;
            }
            await SetRoleColorAsync(new(colorNumber));
        }

        private async Task SetRoleColorAsync(Color color)
        {
            if (Context.User is not SocketGuildUser gUser)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden);
                return;
            }

            var roleName = DiscordUtils.GetCustomColorRoleName(gUser);
            var role = Context.Guild.Roles.FirstOrDefault(x => x.Name == roleName);

            var requiredRole = (await _dbContext.GetGuildByIdAsync(Context.Guild.Id))?.CustomColorPermissionRoleId;
            if (role is null && (requiredRole is null || requiredRole == ulong.MinValue || gUser.Roles.FirstOrDefault(x => x.Id == requiredRole) is null))
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden);
                return;
            }

            if (role is null)
            {
                _logger.LogDebug("{intTag} Creating custom color role {roleName} for user {user} in guild {guild}", GetIntTag(), roleName, gUser.Log(), Context.Guild.Log());
                role = await Context.Guild.CreateRoleAsync(roleName, color: color, isMentionable: false);
                _logger.LogInformation("{intTag} Created custom color role {role} for user {user} in guild {guild}", GetIntTag(), role.Log(), gUser.Log(), Context.Guild.Log());
            }
            else
            {
                _logger.LogDebug("{intTag} Modifying custom color role {role} for user {user} in guild {guild}", GetIntTag(), role.Log(), gUser.Log(), Context.Guild.Log());
                await role.ModifyAsync(x => x.Color = color);
                _logger.LogInformation("{intTag} Modified custom color role {role} for user {user} in guild {guild}", GetIntTag(), role.Log(), gUser.Log(), Context.Guild.Log());
            }

            if (!gUser.Roles.Contains(role))
            {
                _logger.LogDebug("{intTag} Adding custom color role {role} to user {user} in guild {guild}", GetIntTag(), role.Log(), gUser.Log(), Context.Guild.Log());
                await gUser.AddRoleAsync(role);
                _logger.LogInformation("{intTag} Added custom color role {role} to user {user} in guild {guild}", GetIntTag(), role.Log(), gUser.Log(), Context.Guild.Log());
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

            if (role is null)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.NoResults);
                return;
            }

            _logger.LogDebug("{intTag} Deleting custom color role {role} from {guild}", GetIntTag(), role.Log(), Context.Guild.Log());
            await role.DeleteAsync();
            _logger.LogInformation("{intTag} Deleted custom color role {role} from {guild}", GetIntTag(), role.Log(), Context.Guild.Log());
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

            _logger.LogDebug("{intTag} Deleting {roleCount} custom color roles in guild {guild}", GetIntTag(), roleCount, Context.Guild.Log());
            foreach (var role in roles)
                await role.DeleteAsync();
            _logger.LogInformation("{intTag} Deleted {roleCount} custom color roles in guild {guild}", GetIntTag(), roleCount, Context.Guild.Log());
            await RespondEmbedAsync("Deleted Custom Colors", $"Succssfully deleted all **{roleCount}** custom color roles");
        }
        #endregion
    }
}