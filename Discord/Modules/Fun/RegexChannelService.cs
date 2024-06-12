using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Modules.Fun
{
    [Module("fun/regex"), AutoLoadService]
    internal class RegexChannelService : IHostedService
    {
        private readonly ILogger<RegexChannelService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DbService _dbService;

        public RegexChannelService(ILogger<RegexChannelService> logger, DiscordSocketClient client, DbService dbService)
        {
            _logger = logger;
            _client = client;
            _dbService = dbService;
        }

        public Task StartAsync(CancellationToken cancellationToken) //todo: [FEATURE] On edit?
        {
            _client.MessageReceived += CheckForRegexAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived -= CheckForRegexAsync;
            return Task.CompletedTask;
        }

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
                        await userMessage.ReplyAsync($"You {regexChannel.PunishmentMessage}");
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
                if (role is null) //todo: [FEATURE] Notify for this?
                {
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
    }
}
