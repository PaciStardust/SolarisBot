using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Hosting;
using SolarisBot.Database;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Discord.Services
{
    internal sealed class JokeRenamingService : IHostedService
    {
        private readonly ILogger<JokeRenamingService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseContext _dbContext;

        public JokeRenamingService(ILogger<JokeRenamingService> logger, DiscordSocketClient client, DatabaseContext dbContext)
        {
            _logger = logger;
            _client = client;
            _dbContext = dbContext;
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

        private static readonly Regex _amVerification = new(@"\b(?:am|i'?m) +(.+)$", RegexOptions.IgnoreCase);
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

            var guild = await _dbContext.GetGuildByIdAsync(gUser.GuildId);
            if (guild == null || guild.JokeRenameOn == false)
                return;

            var timeOut = guild.JokeTimeouts.FirstOrDefault(x => x.UserId == gUser.Id);
            var currTime = Utils.GetCurrentUnix(_logger);
            if (timeOut != null && timeOut.NextUse > currTime)
                return;

            timeOut ??= new()
                {
                    UserId = gUser.Id,
                    GId = gUser.GuildId
                };

            var cooldown = guild.JokeRenameTimeoutMin >= guild.JokeRenameTimeoutMax
                ? guild.JokeRenameTimeoutMax
                : Utils.Faker.Random.ULong(guild.JokeRenameTimeoutMin, guild.JokeRenameTimeoutMax);
            timeOut.NextUse = currTime + cooldown;

            _dbContext.JokeTimeouts.Update(timeOut);
            var res = await _dbContext.SaveChangesAsync();
            if (res == -1)
                return;

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
