using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Database.Models;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using System.Reflection;
using System.Text.RegularExpressions;

namespace SolarisBot.Discord.Services
{
    [AutoLoadService]
    internal sealed class InteractionHandlerService : IHostedService
    {
        private readonly DiscordSocketClient _client;
        private readonly InteractionService _intService;
        private readonly BotConfig _config;
        private readonly DatabaseService _databaseService;
        private readonly ILogger<InteractionHandlerService> _logger;
        private readonly IServiceProvider _services;

        public InteractionHandlerService(DiscordSocketClient client, InteractionService interactions, BotConfig config, DatabaseService databaseService, ILogger<InteractionHandlerService> logger, IServiceProvider services)
        {
            _client = client;
            _intService = interactions;
            _config = config;
            _databaseService = databaseService;
            _services = services;
            _logger = logger;

            _intService.Log += logMessage => logMessage.Log(_logger);
        }

        /// <summary>
        /// Loads in all interaction modules and registers them
        /// </summary>
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _client.InteractionCreated += HandleInteractionCreated;
            _intService.InteractionExecuted += HandleInteractionExecuted;

            foreach(var type in GetType().Assembly.GetTypes())
            {
                if (!type.IsSubclassOf(typeof(SolarisInteractionModuleBase))) continue;
                var attribute = type.GetCustomAttribute<ModuleAttribute>();
                var moduleNamesText = attribute is null ? "NONE" : string.Join(" + ", attribute.ModuleNames);
                if (attribute?.IsDisabled(_config.DisabledModules) ?? false)
                {
                    _logger.LogDebug("Skipping adding InteractionModule {intModule} from disabled module {module}", type.FullName, moduleNamesText);
                    continue;
                }
                _logger.LogDebug("Adding InteractionModule {intModule} from module {module}", type.FullName, moduleNamesText);
                await _intService.AddModuleAsync(type, _services);
            }

#if DEBUG
            _client.Ready += RegisterInteractionsToMainAsync;
#else
            _client.Ready += RegisterInteractionsGloballyAsync;
#endif
        }

#pragma warning disable IDE0051 // Remove unused private members
        /// <summary>
        /// Registers all interactions to main guild
        /// </summary>
        private async Task RegisterInteractionsToMainAsync()
        {
            _logger.LogInformation("Ready in DEBUG");
            var guild = _client.GetGuild(_config.MainGuild);
            if (guild != null)
            {
                _logger.LogInformation("Registering interactions to guild {guild}", guild.Log());
                await _intService.RegisterCommandsToGuildAsync(_config.MainGuild);
            }
        }

        /// <summary>
        /// Registers all interactions globally
        /// </summary>
        private async Task RegisterInteractionsGloballyAsync()
        {
            _logger.LogInformation("Ready in RELEASE");
            var guild = _client.GetGuild(_config.MainGuild);
            if (guild is not null)
            {
                _logger.LogInformation("Unregistering interactions to guild {guild}", guild.Log());
                await guild.DeleteApplicationCommandsAsync();
            }
            _logger.LogInformation("Registering interactions globally");
            await _intService.RegisterCommandsGloballyAsync();
        }
#pragma warning restore IDE0051 // Remove unused private members

        /// <summary>
        /// Stops the Interaction handler
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StopAsync(CancellationToken cancellationToken)
        {
            _client.InteractionCreated -= HandleInteractionCreated;
            _intService.InteractionExecuted -= HandleInteractionExecuted;
            _client.Ready -= RegisterInteractionsToMainAsync;

            _intService.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles an interaction being created and executes it
        /// </summary>
        private async Task HandleInteractionCreated(SocketInteraction interaction)
        {
            var cmdName = "N/A";
            if (interaction is SocketCommandBase scb)
                cmdName = scb.CommandName;

            var context = new SocketInteractionContext(_client, interaction);
            _logger.LogDebug("Executing interaction \"{interactionName}\"({interactionId}) for user {user} in channel {channel} of guild {guild}", cmdName, interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
            var result = await _intService.ExecuteCommandAsync(context, _services);

            //this will happen when the command cant be found
            if (!result.IsSuccess)
            {
                _logger.LogError("Failed executing interaction \"{interactionName}\"({interactionId}) for user {user} in channel {channel} of guild {guild} => {error}: {reason}", cmdName, interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A", result.Error.ToString()!, result.ErrorReason);
                await context.Interaction.ReplyErrorAsync($"{result.Error!}: {result.ErrorReason}");
            }
        }

        /// <summary>
        /// Handles the result of an interaction
        /// </summary>
        private async Task HandleInteractionExecuted(ICommandInfo cmdInfo, IInteractionContext context, IResult result)
        {
            var record = new DbInteractionRecord()
            {
                InteractionCreatedAt = Convert.ToUInt64(context.Interaction.CreatedAt.ToUniversalTime().ToUnixTimeSeconds()),
                InteractionCompletedAt = Utils.GetCurrentUnix(),
                InteractionId = context.Interaction.Id,
                InteractionType = context.Interaction.Type.ToString(),
                ContextType = context.Interaction.ContextType?.ToString() ?? string.Empty,
                Success = result.IsSuccess,
                ModuleName = cmdInfo.Module.Name,
                GuildId = context.Interaction.GuildId ?? ulong.MinValue,
                ChannelId = context.Interaction.ChannelId ?? ulong.MinValue,
                UserId = context.Interaction.User.Id,
                CommandGroupName = cmdInfo.Module.SlashGroupName,
                CommandName = cmdInfo.Name,
                MethodName = cmdInfo.MethodName,
                Arguments = GetOptionsString(context.Interaction.Data)
            };

            if (result.IsSuccess)
            {
                _logger.LogDebug("Executed interaction \"{interactionModule}\"(Module {module}, Id {interactionId}) for user {user} in channel {channel} of guild {guild}", cmdInfo?.Name ?? "N/A", cmdInfo?.Module.Name ?? "N/A", context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
            }
            else if (result is ExecuteResult exeResult)
            {
                var exception = exeResult.Exception;
                while(exception.InnerException is not null)
                    exception = exception.InnerException;

                record.ErrorType = exception.GetType().Name;
                record.ErrorMessage = exception.Message;
                record.ErrorTrace = record.ErrorTrace;

                _logger.LogError(exeResult.Exception, "Failed to execute interaction \"{interactionModule}\"(Module {module}, Id {interactionId}) for user {user} in channel {channel} of guild {guild}", cmdInfo?.Name ?? "N/A", cmdInfo?.Module.Name ?? "N/A", context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A");
            }
            else
            {
                record.ErrorType = result.Error!.Value.ToString();
                record.ErrorMessage = result.ErrorReason;

                _logger.LogError("Failed to execute interaction \"{interactionModule}\"(Module {module}, Id {interactionId}) for user {user} in channel {channel} of guild {guild} => {error}: {reason}", cmdInfo?.Name ?? "N/A", cmdInfo?.Module.Name ?? "N/A", context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A", result.Error.ToString()!, result.ErrorReason);
            }

            if (!record.Success)
            {
                try
                {
                    await context.Interaction.ReplyErrorAsync($"{record.ErrorType}: {record.ErrorMessage}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed responding to interaction failing \"{interactionModule}\"(Module {module}, Id {interactionId}) for user {user} in channel {channel} of guild {guild} => {error}: {reason}", cmdInfo?.Name ?? "N/A", cmdInfo?.Module.Name ?? "N/A", context.Interaction.Id, context.User.Log(), context.Channel?.Log() ?? "N/A", context.Guild?.Log() ?? "N/A", record.ErrorType, record.ErrorMessage);
                }
            }

            _logger.LogDebug("Saving log of interaction {interactionId} in DB", context.Interaction.Id);
            using var dbCtx = _databaseService.GetContext();
            dbCtx.InteractionRecords.Add(record);
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
                _logger.LogError(err, "Failed saving log of interaction {interactionId} in DB", context.Interaction.Id);
            else
                _logger.LogDebug("Saved log of interaction {interactionId} in DB", context.Interaction.Id);
        }

        /// <summary>
        /// Generates an options string
        /// </summary>
        private static string GetOptionsString(IDiscordInteractionData interactionData)
        {
            if (interactionData is IApplicationCommandInteractionData commandInteractionData)
                return string.Join("|", commandInteractionData.Options.Select(x => $"{x.Name}({string.Join("|", x.Options.Select(y => $"{y.Name}({Regex.Escape(y.Value.ToString() ?? string.Empty)})"))})"));

            if (interactionData is IComponentInteractionData componentInteractionData)
            {
                var contents = new List<string>() { $"customId({componentInteractionData.CustomId})" };
                if ((componentInteractionData.Channels?.Count ?? 0) > 0)
                    contents.Add($"channels({string.Join("|", componentInteractionData.Channels!.Select(x => x.Id))})");
                if ((componentInteractionData.Members?.Count ?? 0) > 0)
                    contents.Add($"members({string.Join("|", componentInteractionData.Members!.Select(x => x.Id))})");
                if ((componentInteractionData.Roles?.Count ?? 0) > 0)
                    contents.Add($"roles({string.Join("|", componentInteractionData.Roles!.Select(x => x.Id))})");
                if ((componentInteractionData.Users?.Count ?? 0) > 0)
                    contents.Add($"users({string.Join("|", componentInteractionData.Users!.Select(x => x.Id))})");
                if (componentInteractionData.Value is not null)
                    contents.Add($"value({Regex.Escape(componentInteractionData.Value)})");
                if ((componentInteractionData.Values?.Count ?? 0) > 0)
                    contents.Add($"values({string.Join("|", componentInteractionData.Values!.Select(x => Regex.Escape(x)))})");

                return string.Join("|", contents);
            }

            return string.Empty;
        }
    }
}
