using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Modules.Fun.Renaming
{
    [Module("fun/renaming"), AutoLoadService]
    internal sealed class RenamingService
    {
        private readonly ILogger<RenamingService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseService _dbService;

        public RenamingService(ILogger<RenamingService> logger, DiscordSocketClient client, DatabaseService dbService)
        {
            _logger = logger;
            _client = client;
            _dbService = dbService;

            _client.MessageReceived += CheckForAutoRename;
        }

        #region Commands
        /// <summary>
        /// Configures renaming in a guild
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="enabled">Enable feature?</param>
        /// <param name="minTimeout">Minimum timeout for renaming</param>
        /// <param name="maxTimeout">Maximum timeout for renaming</param>
        /// <returns>Config on success / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigureRenamingAsync(IGuild guild, bool enabled, ulong minTimeout, ulong maxTimeout)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);

            dbGuild.JokeRenameOn = enabled;
            dbGuild.JokeRenameTimeoutMax = maxTimeout;
            dbGuild.JokeRenameTimeoutMin = minTimeout > maxTimeout ? maxTimeout : minTimeout;

            _logger.LogTrace("Setting joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", enabled, minTimeout, maxTimeout, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", enabled, minTimeout, maxTimeout, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogDebug("Set joke renaming to enabled={role}, mintimeout={minTimeout}, maxtimeout={maxTimeout} in guild {guild}", enabled, minTimeout, maxTimeout, guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }

        /// <summary>
        /// Resets all cooldowns for a guild
        /// </summary>
        /// <param name="guild">Guild to reset</param>
        /// <returns>All removed timeouts on success / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbJokeTimeout[]>, Error<string>, Error<Exception>>> ResetRenamingCooldownsAsync(IGuild guild)
        {
            using var dbCtx = _dbService.GetContext();
            var jokeTimeouts = await dbCtx.JokeTimeouts.ForGuild(guild.Id).ToArrayAsync();
            dbCtx.JokeTimeouts.RemoveRange(jokeTimeouts);

            if (jokeTimeouts.Length == 0)
                return new Error<string>(StandardError.NoResults);

            _logger.LogTrace("Deleting all {delCount} joke timeout cooldowns for guild {guild}", jokeTimeouts.Length, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed deleting all {delCount} joke timeout cooldowns for guild {guild}", jokeTimeouts.Length, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogDebug("Deleted all {delCount} joke timeout cooldowns for guild {guild}", jokeTimeouts.Length, guild.Log());
            return new Success<DbJokeTimeout[]>(jokeTimeouts);
        }
        #endregion

        #region Message Handling
        private static readonly Regex _amVerification = new(@"\b(?:am(?!\s+i)|i'?m)\s+(.+)$", RegexOptions.IgnoreCase);
        /// <summary>
        /// Automatically renames a user after saying "I am..." when enabled
        /// </summary>
        private async Task CheckForAutoRename(SocketMessage message)
        {
            if (message is not IUserMessage userMessage || message.Author.IsWebhook || message.Author.IsBot || message.Author is not IGuildUser gUser)
                return;

            var match = _amVerification.Match(userMessage.CleanContent);
            if (!match.Success)
                return;

            var name = match.Groups[1].Value;

            if (name.Length > 32)
                return;

            using var dbCtx = _dbService.GetContext();
            var guild = await dbCtx.GetGuildByIdAsync(gUser.GuildId);
            if (guild is null || guild.JokeRenameOn == false)
                return;

            var timeOut = await dbCtx.JokeTimeouts.ForGuild(gUser.GuildId).ForUser(gUser.Id).FirstOrDefaultAsync();
            var currTime = Utils.GetCurrentUnix();
            if (timeOut is not null && timeOut.NextUse > currTime)
                return;

            timeOut ??= new()
            {
                UserId = gUser.Id,
                GuildId = gUser.GuildId
            };

            var cooldown = guild.JokeRenameTimeoutMin >= guild.JokeRenameTimeoutMax
                ? guild.JokeRenameTimeoutMax
                : Utils.Faker.Random.ULong(guild.JokeRenameTimeoutMin, guild.JokeRenameTimeoutMax);
            timeOut.NextUse = currTime + cooldown;

            _logger.LogTrace("Setting renaming nextUse for user {user} in guild {guild} to {timeout}", gUser.Log(), gUser.Guild.Log(), timeOut.NextUse);
            dbCtx.JokeTimeouts.Update(timeOut);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed to set renaming nextUse for user {user} in guild {guild} to {timeout}", gUser.Log(), gUser.Guild.Log(), timeOut.NextUse);
                return;
            }
            _logger.LogDebug("Set renaming nextUse for user {user} in guild {guild} to {timeout}", gUser.Log(), gUser.Guild.Log(), timeOut.NextUse);

            var logTimespan = TimeSpan.FromSeconds(cooldown);
            try
            {
                _logger.LogTrace("Changing user {user} nickname to {nickname} in guild {guild}, timeout is {time}", gUser.Log(), name, gUser.Guild.Log(), logTimespan);
                await gUser.ModifyAsync(x => x.Nickname = name);
                _logger.LogDebug("Changed user {user} nickname to {nickname} in guild {guild}, timeout is {time}", gUser.Log(), name, gUser.Guild.Log(), logTimespan);
                await userMessage.ReplyAsync($"Hello {gUser.Mention}, I am {_client.CurrentUser.Username}!");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed changing user {user} nickname to {nickname} in guild {guild}, timeout is {time}", gUser.Log(), name, gUser.Guild.Log(), logTimespan);
            }
        }
        #endregion
    }
}
