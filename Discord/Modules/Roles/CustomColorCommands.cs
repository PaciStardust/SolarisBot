using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Globalization;
using System.Text.RegularExpressions;
using Color = Discord.Color;

namespace SolarisBot.Discord.Modules.Roles
{
    [Module("roles/customcolor"), Group("customcolor", "Tweak your custom color (Requires permission role)"), RequireContext(ContextType.Guild)]
    public sealed class CustomColorCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<CustomColorCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal CustomColorCommands(ILogger<CustomColorCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        #region Create
        [SlashCommand("set-color-rgb", "Set your custom role color via RGB (Requires permission role)"), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task SetRoleColorByRgb(byte red, byte green, byte blue)
            => await SetRoleColorAsync(new(red, green, blue));

        private static readonly Regex _hexCodeValidator = new(@"[A-F0-9]{6}");

        [SlashCommand("set-color-hex", "Set your custom role color via Hex (Requires permission role)"), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task SetRoleColorByHex([MinLength(6), MaxLength(6)] string hex)
        {
            var upperHex = hex.ToUpper();
            if (!_hexCodeValidator.IsMatch(upperHex) || !uint.TryParse(upperHex, NumberStyles.HexNumber, CultureInfo.CurrentCulture, out var colorNumber))
            {
                await Interaction.ReplyErrorAsync($"Failed to convert **{upperHex}** to hex code");
                return;
            }
            await SetRoleColorAsync(new(colorNumber));
        }

        private async Task SetRoleColorAsync(Color color)
        {
            var gUser = GetGuildUser(Context.User);
            var generatedRoleName = DiscordUtils.GetCustomColorRoleName(gUser);
            var customColorRole = Context.Guild.Roles.FirstOrDefault(x => x.Name == generatedRoleName);

            if (customColorRole is null)
            {
                var permissionRole = (await _dbContext.GetGuildByIdAsync(Context.Guild.Id))?.CustomColorPermissionRoleId;
                if (permissionRole is null || permissionRole == ulong.MinValue)
                {
                    await Interaction.ReplyErrorAsync("Custom color roles are not enabled in this guild");
                    return;
                }
                if (gUser.Roles.FirstOrDefault(x => x.Id == permissionRole) is null)
                {
                    await Interaction.ReplyErrorAsync($"You do not have the required role <@&{permissionRole}>");
                    return;
                }

                _logger.LogDebug("{intTag} Creating custom color role {roleName} for user {user} in guild {guild}", GetIntTag(), generatedRoleName, gUser.Log(), Context.Guild.Log());
                customColorRole = await Context.Guild.CreateRoleAsync(generatedRoleName, color: color, isMentionable: false);
                _logger.LogInformation("{intTag} Created custom color role {role} for user {user} in guild {guild}", GetIntTag(), customColorRole.Log(), gUser.Log(), Context.Guild.Log());
            }
            else
            {
                _logger.LogDebug("{intTag} Modifying custom color role {role} for user {user} in guild {guild}", GetIntTag(), customColorRole.Log(), gUser.Log(), Context.Guild.Log());
                await customColorRole.ModifyAsync(x => x.Color = color);
                _logger.LogInformation("{intTag} Modified custom color role {role} for user {user} in guild {guild}", GetIntTag(), customColorRole.Log(), gUser.Log(), Context.Guild.Log());
            }

            if (!gUser.Roles.Contains(customColorRole))
            {
                _logger.LogDebug("{intTag} Adding custom color role {role} to user {user} in guild {guild}", GetIntTag(), customColorRole.Log(), gUser.Log(), Context.Guild.Log());
                await gUser.AddRoleAsync(customColorRole);
                _logger.LogInformation("{intTag} Added custom color role {role} to user {user} in guild {guild}", GetIntTag(), customColorRole.Log(), gUser.Log(), Context.Guild.Log());
            }

            await Interaction.ReplyAsync($"Custom color role has been set to {customColorRole.Mention}", color, isEphemeral: true);
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
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            _logger.LogDebug("{intTag} Deleting custom color role {role} from {guild}", GetIntTag(), role.Log(), Context.Guild.Log());
            await role.DeleteAsync();
            _logger.LogInformation("{intTag} Deleted custom color role {role} from {guild}", GetIntTag(), role.Log(), Context.Guild.Log());
            await Interaction.ReplyAsync("Deleted your custom color role", isEphemeral: true);
        }

        [SlashCommand("delete-all", "[REQUIRES MANAGE ROLES] Delete all custom color roles"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireBotPermission(ChannelPermission.ManageRoles)]
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
        #endregion
    }
}