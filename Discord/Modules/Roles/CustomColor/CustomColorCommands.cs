using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Globalization;
using System.Text.RegularExpressions;
using Color = Discord.Color;

namespace SolarisBot.Discord.Modules.Roles.CustomColor
{
    [Module("roles/customcolor"), Group("customcolor", "Tweak your custom color (Requires permission role)"), RequireContext(ContextType.Guild)]
    public sealed class CustomColorCommands : SolarisInteractionModuleBase
    {
        private readonly CustomColorService _customColorService;

        internal CustomColorCommands(CustomColorService customColorService)
        {
            _customColorService = customColorService;
        }

        #region Create
        [SlashCommand("set-color-rgb", "Set your custom role color via RGB (Requires permission role)"), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task SetRoleColorByRgb
        (
            [Summary(description: "Red amount")] byte red,
            [Summary(description: "Green amount")] byte green,
            [Summary(description: "Blue amount")] byte blue
        )
            => await SetRoleColorAsync(new(red, green, blue));

        private static readonly Regex _hexCodeValidator = new(@"[A-F0-9]{6}");

        [SlashCommand("set-color-hex", "Set your custom role color via Hex (Requires permission role)"), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task SetRoleColorByHex
        (
            [Summary(description: "Hex color code (without #)"), MinLength(6), MaxLength(6)] string hex
        )
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
            var res = await _customColorService.CreateCustomColorRole(Context.Guild, Context.User, color);
            await res.Match(
                success => Interaction.ReplyAsync($"Custom color role has been set to {success.Value.Mention}", color, isEphemeral: true),
                deletedRole => Interaction.ReplyDeletedRoleErrorAsync(deletedRole.Value),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
        #endregion

        #region Delete
        [SlashCommand("delete", "Delete your custom color role"), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task DeleteCustomColorRoleAsync()
        {
            var res = await _customColorService.DeleteCustomColorRole(Context.Guild, Context.User);
            await res.Match(
                success => Interaction.ReplyAsync("Deleted your custom color role", isEphemeral: true),
                none => Interaction.ReplyErrorAsync(GenericError.NoResults),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
        #endregion
    }
}