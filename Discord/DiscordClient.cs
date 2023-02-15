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
            _client.UserVoiceStateUpdated += UserVoiceStateUpdated;

            await _client.LoginAsync(TokenType.Bot, Config.Discord.Token);
            await _client.StartAsync();
        }

        private static async Task UserVoiceStateUpdated(SocketUser user, SocketVoiceState stateOld, SocketVoiceState stateNew)
            => await CheckForTempChannelActions(user, stateOld, stateNew);

        private static async Task MessageReceivedAsync(SocketMessage message)
            => await CheckForAutoRename(message);

        private static async Task ReadyAsync()
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

        internal static async Task ChangeStatus(string status)
        {
            var game = new Game(status);
            await _client.SetActivityAsync(game);
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

        //todo: move
        #region Extras
        private static readonly Regex _amVerification = new(@"\b(?:am|i'?m) +(.+)$", RegexOptions.IgnoreCase);
        private static async Task CheckForAutoRename(SocketMessage arg)
        {
            if (arg is not IUserMessage message || arg.Author is not IGuildUser gUser || gUser.IsBot || gUser.IsWebhook)
                return;

            var match = _amVerification.Match(arg.CleanContent);
            if (!match.Success)
                return;

            var name = match.Groups[1].Value;

            if (name.Length > 32)
                return;

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

        private static async Task CheckForTempChannelActions(SocketUser user, SocketVoiceState stateOld, SocketVoiceState stateNew) //Todo: logging
        {
            if (stateOld.VoiceChannel?.Id == stateNew.VoiceChannel?.Id) //todo: clear empty on restart
                return;

            if (user is not IGuildUser gUser)
                return;

            var guild = gUser.Guild;
            var dbGuild = DbGuild.GetOne(guild.Id) ?? DbGuild.Default;
            var channels = DbVcGen.GetOne(guild.Id);

            //Checking if a temp channel was left
            if (stateOld.VoiceChannel != null)
            {
                var channelMatch = channels.Where(x => x.VChannel == stateOld.VoiceChannel.Id);
                if (channelMatch.Any())
                {
                    var userCount = stateOld.VoiceChannel.ConnectedUsers.Count;
                    if (userCount == 0)
                    {
                        var oldVcGen = channelMatch.First();
                        await stateOld.VoiceChannel.DeleteAsync(); //todo: error checking
                        await (await guild.GetChannelAsync(oldVcGen.TChannel)).DeleteAsync();
                        if (DbMain.Run($"DELETE FROM vcgen WHERE vchannel = {oldVcGen.VChannel}") < 1)
                            Logger.Warning($"VcGen with id {oldVcGen.VChannel} could not be removed");
                    }
                }
            }

            //Was temp channel creator joined and are slots open?
            if (stateNew.VoiceChannel != null && dbGuild.VcGenChannel != null && stateNew.VoiceChannel.Id == dbGuild.VcGenChannel && channels.Count < dbGuild.VcGenMax) //Checks if channel creator vc was entered
            {
                IVoiceChannel? newVoice = null;
                ITextChannel? newText = null;
                var failedCreation = false;

                try
                {
                    var channelName = $"Temp Channel " + (channels.Count + 1);
                    //Creating channels
                    newVoice = await guild.CreateVoiceChannelAsync(channelName);
                    newText = await guild.CreateTextChannelAsync(channelName);
                    //Applying perms
                    var newPerms = stateNew.VoiceChannel.PermissionOverwrites;
                    await newVoice.ModifyAsync(x => x.PermissionOverwrites = new(newPerms));
                    await newText.ModifyAsync(x => x.PermissionOverwrites = new(newPerms));
                    //Moving user
                    await gUser.ModifyAsync(x => x.Channel = new(newVoice));

                    var dbEntry = new DbVcGen(newVoice.Id, newText.Id, gUser.Id);
                    if (!dbEntry.Create())
                    {
                        //todo: log reason
                        failedCreation = true;
                    }

                    await newText.SendMessageAsync("test message"); //todo: actual info, move?
                }
                catch (Exception e)
                {
                    //todo: log reason
                    failedCreation = true;
                }

                //Attempting to delete channels if they exist after error
                if (failedCreation)
                {
                    try
                    {
                        if (newVoice != null)
                            await newVoice.DeleteAsync();
                        if (newText != null)
                            await newText.DeleteAsync();
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e);
                        //todo: write error into a channel
                    }
                }
            }
            return;
        }
        #endregion
    }
}
