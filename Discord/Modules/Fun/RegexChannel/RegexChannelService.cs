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

namespace SolarisBot.Discord.Modules.Fun.RegexChannel
{
    [Module("fun/regex"), AutoLoadService]
    internal class RegexChannelService
    {
        private readonly ILogger<RegexChannelService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseService _dbService;

        public RegexChannelService(ILogger<RegexChannelService> logger, DiscordSocketClient client, DatabaseService dbService)
        {
            _logger = logger;
            _client = client;
            _dbService = dbService;

            _client.MessageReceived += CheckForRegexAsync;
            _client.MessageUpdated += CheckForRegexOnEditAsync;
        }

        #region Commands
        /// <summary>
        /// Creates a regex channel
        /// </summary>
        /// <param name="channel">Target channel</param>
        /// <param name="guild">Target guild</param>
        /// <param name="regex">Regex to use</param>
        /// <param name="punishmentRole">Role to apply on fail</param>
        /// <param name="punishmentMsg">Message sent on fail</param>
        /// <param name="deleteMsg">Delete failed message</param>
        /// <param name="punishmentTimeout">Timeout duration on fail</param>
        /// <returns>Created channel / Error</returns>
        internal async Task<OneOf<Success<DbRegexChannel>, Error<string>, Error<Exception>>> AddRegexChannel(IChannel channel, IGuild guild, string regex, IRole? punishmentRole, string punishmentMsg, bool deleteMsg, ulong punishmentTimeout)
        {
            using var dbCtx = _dbService.GetContext();

            if (string.IsNullOrWhiteSpace(regex))
                return new Error<string>("Regular Expression can not be empty, to disable use remove command");

            try
            {
                _ = new Regex(regex);
            }
            catch
            {
                return new Error<string>($"Failed to validate RegEx: {regex}");
            }

            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id, x => x.Include(y => y.RegexChannels));
            var dbChannel = dbGuild.RegexChannels.FirstOrDefault(x => x.ChannelId == channel.Id)
                ?? new DbRegexChannel() { GuildId = guild.Id, ChannelId = channel.Id };

            dbChannel.Regex = regex;
            dbChannel.AppliedRoleId = punishmentRole?.Id ?? ulong.MinValue;
            dbChannel.PunishmentMessage = punishmentMsg;
            dbChannel.PunishmentDelete = deleteMsg;
            dbChannel.PunishmentTimeout = punishmentTimeout;

            dbCtx.RegexChannels.Update(dbChannel);
            _logger.LogDebug("Setting regex to rx={channelRegex}, role={punishmentRole}, msg={punishmentMsg}, del={delete}, timeout={timeout} for channel {channel} in guild {guild}", dbChannel.Regex, dbChannel.PunishmentTimeout, dbChannel.AppliedRoleId, dbChannel.PunishmentMessage, dbChannel.PunishmentDelete, channel.Log(), guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting regex to rx={channelRegex}, role={punishmentRole}, msg={punishmentMsg}, del={delete}, timeout={timeout} for channel {channel} in guild {guild}", dbChannel.Regex, dbChannel.PunishmentTimeout, dbChannel.AppliedRoleId, dbChannel.PunishmentMessage, dbChannel.PunishmentDelete, channel.Log(), guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Set regex to rx={channelRegex}, role={punishmentRole}, msg={punishmentMsg}, del={delete}, timeout={timeout} for channel {channel} in guild {guild}", dbChannel.Regex, dbChannel.PunishmentTimeout, dbChannel.AppliedRoleId, dbChannel.PunishmentMessage, dbChannel.PunishmentDelete, channel.Log(), guild.Log());

            return new Success<DbRegexChannel>(dbChannel);
        }

        /// <summary>
        /// Gets a list of all regex channels for a guild
        /// </summary>
        /// <param name="guildId">ID of guild</param>
        /// <returns>Array of all regex channels</returns>
        internal async Task<DbRegexChannel[]> GetRegexChannelsAsync(ulong guildId)
        {
            using var dbCtx = _dbService.GetContext();

            var regexChannels = await dbCtx.RegexChannels.ForGuild(guildId).ToArrayAsync();
            return regexChannels;
        }

        /// <summary>
        /// Deletes a regexChannel associated with a an ID or a channel
        /// </summary>
        /// <param name="idIsChannel">Indicates if ID is for channel or DB</param>
        /// <param name="targetId">Used ID</param>
        /// <param name="guild">Guild to delete in</param>
        /// <returns>Array of deleted channels / Error string / Exception</returns>
        internal async Task<OneOf<Success<DbRegexChannel[]>, Error<string>, Error<Exception>>> DeleteRegexChannelAsync(bool idIsChannel, ulong targetId, IGuild guild)
        {
            using var dbCtx = _dbService.GetContext();

            var query = dbCtx.RegexChannels.ForGuild(guild.Id);
            query = idIsChannel
                ? query.ForChannel(targetId)
                : query.Where(x => x.RegexChannelId == targetId);

            var regexChannels = await query.ToArrayAsync();
            if (regexChannels.Length == 0)
                return new Error<string>(StandardError.NoResults);

            dbCtx.RegexChannels.RemoveRange(regexChannels);
            _logger.LogDebug("Removing {channelCount} regex channels in guild {guild}", regexChannels.Length, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed removing {channelCount} regex channels in guild {guild}", regexChannels.Length, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Removed {channelCount} regex channels in guild {guild}", regexChannels.Length, guild.Log());
            return new Success<DbRegexChannel[]>(regexChannels);
        }
        #endregion

        #region Message Handling
        /// <summary>
        /// Checks if the message needs to be regex checked and then applies punishments if checks fail
        /// </summary>
        /// <param name="message">Message to check</param>
        private async Task CheckForRegexAsync(SocketMessage message)
        {
            if (message is not IUserMessage userMessage || message.Author.IsWebhook || message.Author.IsBot || message.Author is not IGuildUser gUser)
                return;

            using var dbCtx = _dbService.GetContext();
            var regexChannel = await dbCtx.RegexChannels.ForChannel(message.Channel.Id).FirstOrDefaultAsync();

            if (regexChannel is null)
                return;

            try
            {
                if (Regex.IsMatch(message.CleanContent, regexChannel.Regex))
                    return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to compile regex {regex}", regexChannel);
                return;
            }

            if (!string.IsNullOrWhiteSpace(regexChannel.PunishmentMessage))
            {
                try
                {
                    if (regexChannel.PunishmentDelete)
                    {
                        _logger.LogDebug("Sending message to regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                        await message.Channel.SendMessageAsync($"{message.Author.Mention} {regexChannel.PunishmentMessage}");
                        _logger.LogInformation("Sent message to regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                    }
                    else
                    {
                        _logger.LogDebug("Responding to regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                        await userMessage.ReplyAsync(regexChannel.PunishmentMessage);
                        _logger.LogInformation("Responded to regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to reply to regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                }
            }

            if (regexChannel.PunishmentDelete)
            {
                try
                {
                    _logger.LogDebug("Deleting message of regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                    await message.DeleteAsync();
                    _logger.LogInformation("Deleted message of regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to delete message of regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                }
            }

            if (regexChannel.PunishmentTimeout > 0)
            {
                try
                {
                    _logger.LogDebug("Timing out for regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                    await gUser.SetTimeOutAsync(TimeSpan.FromSeconds(regexChannel.PunishmentTimeout));
                    _logger.LogInformation("Timed out for regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed timing out for regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                }
            }

            if (regexChannel.AppliedRoleId > 0)
            {
                var role = gUser.Guild.FindRole(regexChannel.AppliedRoleId);
                if (role is null)
                {
                    _logger.LogDebug("Could not locate RegexRole for RegexChannel {channel} with id {roleId}", regexChannel, regexChannel.AppliedRoleId);
                    return;
                }
                else
                {
                    try
                    {
                        _logger.LogDebug("Applying role {role} for regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", role.Log(), regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                        await gUser.AddRoleAsync(role);
                        _logger.LogInformation("Applied role {role} for regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", role.Log(), regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed applying role {role} for regex {regex} violation by user {user} in channel {channel} of guild {guild} with message {message}", role.Log(), regexChannel, message.Author.Log(), message.Channel.Log(), gUser.Guild.Log(), message.CleanContent);
                    }
                }
            }
        }

        /// <summary>
        /// Checks if the message needs to be regex checked and then applies punishments if checks fail
        /// </summary>
        /// <param name="oldMessage">Old message</param>
        /// <param name="newMessage">New message</param>
        /// <param name="channel">Channel</param>
        private Task CheckForRegexOnEditAsync(Cacheable<IMessage, ulong> oldMessage, SocketMessage newMessage, ISocketMessageChannel channel)
            => CheckForRegexAsync(newMessage);
        #endregion
    }
}
