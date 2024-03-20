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
        private async Task CheckForSpellErrorsAsync(SocketMessage message)
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

            var dbCtx = _provider.GetRequiredService<DatabaseContext>();
            var guild = await dbCtx.GetGuildByIdAsync(gUser.GuildId);
            if (guild is null || guild.SpellcheckRoleId == ulong.MinValue || gUser.Guild.FindRole(guild.SpellcheckRoleId) is null) //todo: [FEATURE] Notify for this?
                return;

            if (!gUser.RoleIds.Contains(guild.SpellcheckRoleId))
                return;

            await userMessage.ReplyAsync($"You misspelled the following: {string.Join(", ", errors)}");
        }
    }
}
