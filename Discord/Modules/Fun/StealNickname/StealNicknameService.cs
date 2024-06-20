using Discord;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Fun.StealNickname
{
    [Module("fun/stealnickname"), AutoLoadService]
    internal class StealNicknameService
    {
        private readonly ILogger<StealNicknameService> _logger;
        private readonly DatabaseService _dbService;
        public StealNicknameService(ILogger<StealNicknameService> logger, DatabaseService dbService)
        {
            _logger = logger;
            _dbService = dbService;
        }

        #region Commands
        /// <summary>
        /// Configure nickname stealing
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="enabled">Feature enabled?</param>
        /// <returns>Config on success / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigureStealNicknameAsync(IGuild guild, bool enabled)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);

            dbGuild.StealNicknameOn = enabled;

            _logger.LogDebug("Setting nickname stealing to {enabled} in guild {guild}", dbGuild.StealNicknameOn, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting nickname stealing to {enabled} in guild {guild}", dbGuild.StealNicknameOn, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Set nickname stealing to {enabled} in guild {guild}", dbGuild.StealNicknameOn, guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }

        /// <summary>
        /// Transfers a random letter from one name to another
        /// </summary>
        /// <param name="executingUser">User receiving letter</param>
        /// <param name="targetUser">User losing letter</param>
        /// <param name="guild">Guild of execution</param>
        /// <returns>Original name of executor, of target, moved letter on success / Error sting / Exception</returns>
        internal async Task<OneOf<Success<(string, string, char)>, Error<string>, Error<Exception>>> StealNicknameAsync(IUser executingUser, IUser targetUser)
        {
            if (executingUser is not IGuildUser executingGuildUser)
                return new Error<string>("Could not convert executing user to guild user");
            var executingName = executingGuildUser.DisplayName;
            if (executingName.Length >= 32) //Max length for nicknames
                return new Error<string>("Your name is too long for stealing");

            if (targetUser is not IGuildUser targetGuildUser)
                return new Error<string>("Could not convert target user to guild user");
            var targetName = targetGuildUser.DisplayName;
            if (targetName.Length <= 1)
                return new Error<string>("Target name is too short for stealing");

            var guild = executingGuildUser.Guild;

            if (executingUser.Id == targetUser.Id)
                return new Error<string>("You can not steal from yourself");
            if (targetUser.IsBot || targetUser.IsWebhook)
                return new Error<string>("You can only steal from humans");
            if (targetUser.Id == guild.OwnerId)
                return new Error<string>("You can not steal from guild owner");
            if (executingUser.Id == guild.OwnerId)
                return new Error<string>("Owners can not steal");

            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetGuildByIdAsync(guild.Id);

            if (!dbGuild?.StealNicknameOn ?? true)
                return new Error<string>("Nickname stealing is not enabled in this guild");

            var stealIndex = Utils.Faker.Random.Int(0, targetName.Length - 1);
            var stolenLetter = targetName[stealIndex];
            var gTargetNameNew = targetName.Remove(stealIndex, 1);
            var insertIndex = Utils.Faker.Random.Int(0, executingName.Length);
            var gNameNew = insertIndex == executingName.Length ? executingName + stolenLetter
                : executingName.Insert(insertIndex, stolenLetter.ToString());

            try
            {
                _logger.LogDebug("Renaming user {user} => {renamed} and {targetUser} => {targetRenamed} after stealing nick", executingGuildUser.Log(), gNameNew, targetGuildUser.Log(), gTargetNameNew);
                await executingGuildUser.ModifyAsync(x => x.Nickname = gNameNew);
                await targetGuildUser.ModifyAsync(x => x.Nickname = gTargetNameNew);
                _logger.LogInformation("Renamed user {user} => {renamed} and {targetUser} => {targetRenamed} after stealing nick", executingGuildUser.Log(), gNameNew, targetGuildUser.Log(), gTargetNameNew);
                return new Success<(string, string, char)>((executingName, targetName, stolenLetter));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed renaming user {user} => {renamed} and {targetUser} => {targetRenamed} after stealing nick", executingGuildUser.Log(), gNameNew, targetGuildUser.Log(), gTargetNameNew);
                return new Error<Exception>(ex);
            }
        }
        #endregion
    }
}
