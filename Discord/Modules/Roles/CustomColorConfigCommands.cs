using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Roles
{
    [Module("roles/customcolor")]
    public sealed class CustomColorConfigCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<CustomColorConfigCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal CustomColorConfigCommands(ILogger<CustomColorConfigCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("cfg-customcolor", "[MANAGE ROLES ONLY] Set up custom color creation"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task ConfigureCustomColorAsync
        (
            [Summary(description: "[Opt] Required role (none to disable)")] IRole? role = null
        )
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.CustomColorPermissionRoleId = role?.Id ?? ulong.MinValue;

            _logger.LogDebug("{intTag} Setting custom colors to role={role} in guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set custom colors to role={role} in guild {guild}", GetIntTag(), role?.Log() ?? "0", Context.Guild.Log());
            await Interaction.ReplyAsync($"Custom color creation is currently **{(role is not null ? "enabled" : "disabled")}**\n\nCreation Role: **{role?.Mention ?? "None"}**");
        }
    }
}
