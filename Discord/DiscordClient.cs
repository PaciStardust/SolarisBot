using Discord.Interactions;
using Discord.WebSocket;
using Discord;
using Microsoft.Extensions.DependencyInjection;
using System.Text.RegularExpressions;
using SolarisBot.Database;
using System.Runtime.CompilerServices;

namespace SolarisBot.Discord
{
    internal static class DiscordClient
    {
        private static readonly DiscordSocketClient _client = new(new()
        {
            GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent,
            UseInteractionSnowflakeDate = false
        });
        private static readonly IServiceProvider _services = ConfigureServices();

        /// <summary>
        /// Starts up the discord bot
        /// </summary>
        internal static async Task Start()
        {
            await _services.GetRequiredService<InteractionHandler>().InitializeAsync();

            _client.Log += Log;
            _client.Ready += ReadyAsync;
            _client.MessageReceived += MessageReceivedAsync;

            await _client.LoginAsync(TokenType.Bot, Config.Discord.Token);
            await _client.StartAsync();
        }

        private static readonly Regex _amVerification = new(@"\b(?:am|i'?m) +(.+)$");
        private static async Task MessageReceivedAsync(SocketMessage arg)
        {
            await CheckForAutoRename(arg);
        }

        internal static async Task ReadyAsync()
            => await UpdateCommandsGuild();

        /// <summary>
        /// Registers all commands in a guild
        /// </summary>
        internal static async Task UpdateCommandsGuild(ulong guildId = 0)
        {
            if (guildId == 0) guildId = Config.Discord.MainGuild;
            Logger.Info("Reloading all commands in guild " + guildId);
            await _services.GetRequiredService<InteractionService>().RegisterCommandsToGuildAsync(guildId, true);
        }

        /// <summary>
        /// Registers all commands globally, to be called from command
        /// </summary>
        internal static async Task UpdateCommands()
        {
            Logger.Info("Globally reloading all commands, this may take an hour to take effect");
            await _services.GetRequiredService<InteractionService>().RegisterCommandsGloballyAsync(true);
        }

        /// <summary>
        /// Logger proxy
        /// </summary>
        private static Task Log(LogMessage msg)
        {
            Logger.Log(msg);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Configures Services for DI
        /// </summary>
        private static ServiceProvider ConfigureServices() => new ServiceCollection()
            .AddSingleton(_client)
            .AddSingleton<InteractionService>()
            .AddSingleton<InteractionHandler>()
            .BuildServiceProvider();

        #region Extras
        private static async Task CheckForAutoRename(SocketMessage arg)
        {
            if (arg is not IUserMessage message || arg.Author is not IGuildUser gUser || gUser.IsBot || gUser.IsWebhook)
                return;

            var match = _amVerification.Match(arg.Content);
            if (!match.Success)
                return;

            var name = match.Groups[1].Value;

            if (name.Length > 32)
            {
                bool cutExtra = name[32] != ' ';
                name = name[..32];

                if (cutExtra)
                {
                    int index = name.LastIndexOf(' ');
                    if (index == -1)
                        return;
                    name = name[..index];
                }
            }

            var guild = DbGuild.GetOne(gUser.Guild.Id) ?? DbGuild.Default;
            if (!guild.Renaming)
                return;

            try
            {
                var bUser = await gUser.Guild.GetUserAsync(_client.CurrentUser.Id);
                await gUser.ModifyAsync(x => x.Nickname = name);
                await message.ReplyAsync($"Hi {gUser.Mention}, I'm {bUser.DisplayName ?? _client.CurrentUser.Mention}!");
            }
            catch
            {
                return;
            }
        } 
        #endregion
    }
}
