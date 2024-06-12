using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Modules.Fun
{
    [Module("fun/spellcheck"), AutoLoadService]
    internal sealed class SpellcheckService : IHostedService
    {
        private readonly ILogger<SpellcheckService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DbService _dbService;
        private readonly HashSet<string> _words = new();
        private readonly BotConfig _botConfig;

        public SpellcheckService(ILogger<SpellcheckService> logger, DiscordSocketClient client, DbService dbService, BotConfig botConfig)
        {
            _logger = logger;
            _client = client;
            _dbService = dbService;
            _botConfig = botConfig;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                var dictPath = Path.Combine(Utils.PathConfigFile, _botConfig.DictionaryFile);
                var words = File.ReadLines(dictPath);
                foreach (var item in words)
                    _words.Add(item);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading dictionary");
            }

            _client.MessageReceived += CheckForSpellErrorsAsync;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.MessageReceived -= CheckForSpellErrorsAsync;
            return Task.CompletedTask;
        }

        private static readonly Regex _nonWordChecker = new(@"[^a-zA-Z]+");
        private static readonly Regex _specialFilter = new(@"(?:<[^>]+>|https?:\/\/[^ ]+)");
        /// <summary>
        /// Annoys user for spelling mistakes lol
        /// </summary>
        private async Task CheckForSpellErrorsAsync(SocketMessage message) //todo: [FEATURE] On edit?
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

                if (!_words.Any(x => x.Equals(word, StringComparison.OrdinalIgnoreCase)))
                    errors.Add(word);
            }

            if (errors.Count == 0)
                return;

            using var dbCtx = _dbService.GetContext();
            var guild = await dbCtx.GetGuildByIdAsync(gUser.GuildId);
            if (guild is null || guild.SpellcheckRoleId == ulong.MinValue || gUser.Guild.FindRole(guild.SpellcheckRoleId) is null) //todo: [FEATURE] Notify for this?
                return;

            if (!gUser.RoleIds.Contains(guild.SpellcheckRoleId))
                return;

            await userMessage.ReplyAsync($"You misspelled the following: {string.Join(", ", errors)}");
        }
    }
}
