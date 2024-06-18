using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Modules.Fun.Spellcheck
{
    [Module("fun/spellcheck"), AutoLoadService]
    internal sealed class SpellcheckService
    {
        private readonly ILogger<SpellcheckService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly DatabaseService _dbService;
        private readonly HashSet<string> _words = [];
        private readonly BotConfig _botConfig;

        public SpellcheckService(ILogger<SpellcheckService> logger, DiscordSocketClient client, DatabaseService dbService, BotConfig botConfig)
        {
            _logger = logger;
            _client = client;
            _dbService = dbService;
            _botConfig = botConfig;

            _client.Ready += OnClientReady;
        }

        /// <summary>
        /// Loads the dictionary and subscribes to spellcheck message handöer
        /// </summary>
        /// <returns></returns>
        private Task OnClientReady()
        {
            try
            {
                _logger.LogDebug("Loading dictionary for spellcheck");
                var dictPath = Path.Combine(Utils.PathConfigDirectory, _botConfig.DictionaryFile);
                var words = File.ReadLines(dictPath);
                foreach (var item in words)
                    _words.Add(item);
                _logger.LogDebug("Loaded dictionary for spellcheck");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed loading dictionary");
            }

            _client.MessageReceived += CheckForSpellErrorsAsync;
            return Task.CompletedTask;
        }

        #region Commands
        /// <summary>
        /// Configures spellchecking in a guild
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="role">Role for spellchecking</param>
        /// <returns>GuildConfig on success / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigureSpellcheckAsync(IGuild guild, IRole? role)
        {
            using var dbCtx = _dbService.GetContext();
            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);
            dbGuild.SpellcheckRoleId = role?.Id ?? ulong.MinValue;

            _logger.LogDebug("Setting spellcheck-role to role {role} for guild {guild}",role?.Log() ?? "0", guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting spellcheck-role to role {role} for guild {guild}", role?.Log() ?? "0", guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Set spellcheck-role to role {role} for guild {guild}", role?.Log() ?? "0", guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }

        #endregion

        #region Message Handling
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
        #endregion
    }
}
