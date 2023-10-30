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
    [Module("fun/renaming"), AutoLoad]
    internal sealed class RenamingService : IHostedService
    {
        private readonly ILogger<RenamingService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _provider;

        public RenamingService(ILogger<RenamingService> logger, DiscordSocketClient client, IServiceProvider provider)
        {
            _logger = logger;
            _client = client;
            _provider = provider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived += CheckForAutoRename;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

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

            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var guild = await dbCtx.GetGuildByIdAsync(gUser.GuildId, x => x.Include(y => y.JokeTimeouts));
            if (guild is null || guild.JokeRenameOn == false)
                return;

            var timeOut = guild.JokeTimeouts.FirstOrDefault(x => x.UserId == gUser.Id);
            var currTime = Utils.GetCurrentUnix(_logger);
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

            _logger.LogDebug("Setting renaming nextUse for user {user} in guild {guild} to {timeout}", gUser.Log(), gUser.Guild.Log(), timeOut.NextUse);
            dbCtx.JokeTimeouts.Update(timeOut);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed to set renaming nextUse for user {user} in guild {guild} to {timeout}", gUser.Log(), gUser.Guild.Log(), timeOut.NextUse);
                return;
            }
            _logger.LogInformation("Set renaming nextUse for user {user} in guild {guild} to {timeout}", gUser.Log(), gUser.Guild.Log(), timeOut.NextUse);

            var logTimespan = TimeSpan.FromSeconds(cooldown);
            try
            {
                _logger.LogDebug("Changing user {user} nickname to {nickname} in guild {guild}, timeout is {time}", gUser.Log(), name, gUser.Guild.Log(), logTimespan);
                await gUser.ModifyAsync(x => x.Nickname = name);
                _logger.LogDebug("Changed user {user} nickname to {nickname} in guild {guild}, timeout is {time}", gUser.Log(), name, gUser.Guild.Log(), logTimespan);
                await userMessage.ReplyAsync($"Hello {gUser.Mention}, I am {_client.CurrentUser.Username}!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed changing user {user} nickname to {nickname} in guild {guild}, timeout is {time}", gUser.Log(), name, gUser.Guild.Log(), logTimespan);
            }
        }
    }
}
