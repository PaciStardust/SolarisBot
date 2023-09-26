using Bogus;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;

namespace SolarisBot.Discord.Commands
{
    [RequireContext(ContextType.Guild)]
    public sealed class GrouplessCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<GrouplessCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal GrouplessCommands(ILogger<GrouplessCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }
        protected override ILogger? GetLogger() => _logger;

        [SlashCommand("vouch", "Vouch for a user"), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task VouchUserAsync(IUser user)
        {
            var dbGuild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);

            if (Context.User is not SocketGuildUser gUser || user is not SocketGuildUser gTargetUser || dbGuild == null //Not a guild or no dbguild
                || dbGuild.VouchPermissionRoleId == 0 || dbGuild.VouchRoleId == 0 //Vouching not set up
                || gUser.Roles.FirstOrDefault(x => x.Id == dbGuild.VouchPermissionRoleId) == null) //User can vouch
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden, isEphemeral: true);
                return;
            }

            if (gTargetUser.Roles.FirstOrDefault(x => x.Id == dbGuild.VouchRoleId) != null)
            {
                await RespondEmbedAsync("Already Vouched", $"{gTargetUser.Mention} has already been vouched", isEphemeral: true);
                return;
            }

            try
            {
                _logger.LogDebug("{intTag} Giving vouch role to user {targetUserData}, has been vouched({vouchRoleId}) for in {guild} by {userData}", GetIntTag(), gTargetUser.Log(), dbGuild.VouchRoleId, Context.Guild.Log(), gUser.Log());
                await gTargetUser.AddRoleAsync(dbGuild.VouchRoleId);
                _logger.LogInformation("{intTag} Gave vouch role to user {targetUserData}, has been vouched({vouchRoleId}) for in {guild} by {userData}", GetIntTag(),gTargetUser.Log(), dbGuild.VouchRoleId, Context.Guild.Log(), gUser.Log());
                await RespondEmbedAsync("Vouch Successful", $"Vouched for {gTargetUser.Mention}, welcome to the server!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{intTag} Failed to give vouch role to user {targetUserData}, has been vouched({vouchRoleId}) for in {guild} by {userData}", GetIntTag(), gTargetUser.Log(), dbGuild.VouchRoleId, Context.Guild.Log(), gUser.Log());
                await RespondErrorEmbedAsync(ex);
            }
        }

        [SlashCommand("magic", "Use magic"), RequireBotPermission(ChannelPermission.ManageRoles)]
        public async Task UseMagicAsync()
        {
            var dbGuild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);

            if (dbGuild == null || dbGuild.MagicRoleId == 0)
            {
                await RespondErrorEmbedAsync(EmbedGenericErrorType.Forbidden, isEphemeral: true);
                return;
            }

            var currentTime = Utils.GetCurrentUnix(_logger);
            if (currentTime < dbGuild.MagicRoleNextUse)
            {
                await RespondErrorEmbedAsync("Mana Exhausted", $"There is currently not enough mana to use magic, please wait until <t:{dbGuild.MagicRoleNextUse}:R>", isEphemeral: true);
                return;
            }

            try
            {
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
                _dbContext.Guilds.Update(dbGuild);

                if (await _dbContext.TrySaveChangesAsync() == -1)
                {
                    _logger.LogWarning("{intTag} Failed to use Magic({magicRoleId}) in guild {guild}, next use updating to {nextUse}", GetIntTag(), dbGuild.MagicRoleId, Context.Guild.Log(), dbGuild.MagicRoleNextUse);
                    await RespondErrorEmbedAsync(EmbedGenericErrorType.DatabaseError);
                    return;
                }
                _logger.LogInformation("{intTag} Used Magic({magicRoleId}) in guild {guild}, next use updating to {nextUse}", GetIntTag(), dbGuild.MagicRoleId, Context.Guild.Log(), dbGuild.MagicRoleNextUse);
                await RespondEmbedAsync("Magic Used", $"Magic has been used, <@&{dbGuild.MagicRoleId}> feels different now", color);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "{intTag} Failed to use magic({magicRoleId} in guild {guild}", GetIntTag(), dbGuild.MagicRoleId, Context.Guild.Log());
                await RespondErrorEmbedAsync(ex);
            }
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
