using Bogus;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using Color = Discord.Color;

namespace SolarisBot.Discord.Modules.Roles
{
    [Module("roles/magic")]
    public sealed class MagicCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<MagicCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal MagicCommands(ILogger<MagicCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("cfg-magic", "[MANAGE ROLES ONLY] Set up magic role"), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
        public async Task ConfigureMagicAsync
        (
            [Summary(description: "[Opt] Magic role (none to disable)")] IRole? role = null,
            [Summary(description: "[Opt] Command cooldown (in sec)")] ulong timeout = 1800, 
            [Summary(description: "[Opt] Automatically rename role?")] bool renaming = false
        )
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.MagicRoleId = role?.Id ?? ulong.MinValue;
            guild.MagicRoleNextUse = ulong.MinValue;
            guild.MagicRoleTimeout = timeout >= ulong.MinValue ? timeout : ulong.MinValue;
            guild.MagicRoleRenameOn = renaming;

            _logger.LogDebug("{intTag} Setting magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", GetIntTag(), role?.Log() ?? "0", guild.MagicRoleTimeout, guild.MagicRoleRenameOn, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set magic to role={role}, timeout={magicTimeout}, rename={magicRename} in guild {guild}", GetIntTag(), role?.Log() ?? "0", guild.MagicRoleTimeout, guild.MagicRoleRenameOn, Context.Guild.Log());
            await Interaction.ReplyAsync($"Magic is currently **{(role is not null ? "enabled" : "disabled")}**\n\nRole: **{role?.Mention ?? "None"}**\nTimeout: **{guild.MagicRoleTimeout} seconds**\nRenaming: **{guild.MagicRoleRenameOn}**");
        }

        [SlashCommand("magic", "Use magic"), RequireBotPermission(GuildPermission.ManageRoles)]
        public async Task UseMagicAsync()
        {
            var dbGuild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);

            if (dbGuild is null || dbGuild.MagicRoleId == ulong.MinValue)
            {
                await Interaction.ReplyErrorAsync("Magic is not enabled in this guild");
                return;
            }
            if (FindRole(dbGuild.MagicRoleId) is null)
            {
                await Interaction.ReplyDeletedRoleErrorAsync("Magic");
                return;
            }

            var currentTime = Utils.GetCurrentUnix();
            if (currentTime < dbGuild.MagicRoleNextUse)
            {
                await Interaction.ReplyErrorAsync($"There is currently not enough mana to use magic, please wait until <t:{dbGuild.MagicRoleNextUse}:R>");
                return;
            }

            _logger.LogDebug("{intTag} Using Magic({magicRoleId}) in guild {guild}, next use updating to {nextUse}", GetIntTag(), dbGuild.MagicRoleId, Context.Guild.Log(), dbGuild.MagicRoleNextUse);
            var faker = Utils.Faker;
            var role = Context.Guild.GetRole(dbGuild.MagicRoleId);
            var color = new Color(faker.Random.Byte(), faker.Random.Byte(), faker.Random.Byte());
            await role.ModifyAsync(x =>
            {
                x.Name = dbGuild.MagicRoleRenameOn ? GenerateMagicName(faker) : x.Name;
                x.Color = color;
            });

            dbGuild.MagicRoleNextUse = currentTime + dbGuild.MagicRoleTimeout;
            _dbContext.GuildConfigs.Update(dbGuild);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Used Magic({magicRoleId}) in guild {guild}, next use updating to {nextUse}", GetIntTag(), dbGuild.MagicRoleId, Context.Guild.Log(), dbGuild.MagicRoleNextUse);
            await Interaction.ReplyAsync($"Magic has been used, <@&{dbGuild.MagicRoleId}> feels different now", color);
        }

        /// <summary>
        /// Generates a random name for the magic role
        /// </summary>
        private static string GenerateMagicName(Faker faker)
        {
            var num = faker.Random.Byte(0, 3);
            if (num == 0)
            {
                var adjective = faker.Hacker.Adjective();
                return $"{adjective[0].ToString().ToUpper()}{adjective[1..]} {faker.Name.FirstName()}";
            }
            else if (num == 1)
                return $"{faker.Commerce.ProductAdjective()} {faker.Name.FirstName()}";
            else
                return faker.Commerce.ProductName();
        }
    }
}
