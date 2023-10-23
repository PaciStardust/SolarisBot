using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.Hosting;
using SolarisBot.Database;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace SolarisBot.Discord.Services
{
    internal sealed class SpellcheckService : IHostedService, IAutoloadService
    {
        private readonly ILogger<SpellcheckService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _provider;
        private readonly HashSet<string> _words = new();

        public SpellcheckService(ILogger<SpellcheckService> logger, DiscordSocketClient client, IServiceProvider provider)
        {
            _logger = logger;
            _client = client;
            _provider = provider;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var words = File.ReadLines(Utils.PathDictionaryFile);
                foreach (var item in words)
                    _words.Add(item.ToLower());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading dictionary"); //todo: [FEATURE] Disable modules for whole bot?
            }

            _client.MessageReceived += CheckForSpellErrors;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private static readonly Regex _nonWordChecker = new(@"[^a-zA-Z]+");
        private static readonly Regex _specialFilter = new(@"(?:<[^>]+>|https?:\/\/[^ ]+)");
        /// <summary>
        /// Annoys user for spelling mistakes lol
        /// </summary>
        private async Task CheckForSpellErrors(SocketMessage message)
        {
            if (message is not IUserMessage userMessage || message.Author.IsWebhook || message.Author.IsBot || message.Author is not IGuildUser gUser)
                return;

            var cleaned = _specialFilter.Replace(message.Content, string.Empty);
            var words = _nonWordChecker.Split(cleaned);
            
            var errors = new List<string>();
            foreach (var word in words)
            {
                if (string.IsNullOrWhiteSpace(word))
                    continue;

                if (!_words.Contains(word.ToLower()))
                    errors.Add(word);
            }

            if (errors.Count == 0)
                return;

            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var guild = await dbCtx.GetGuildByIdAsync(gUser.GuildId);
            if (guild is null || guild.SpellcheckRoleId == 0)
                return;

            if (!gUser.RoleIds.Contains(guild.SpellcheckRoleId))
                return;

            await userMessage.ReplyAsync($"You misspelled the following: {string.Join(", ", errors)}");
        }
    }
}
