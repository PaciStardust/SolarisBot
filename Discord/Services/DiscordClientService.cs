using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;

namespace SolarisBot.Discord.Services
{
    internal sealed class DiscordClientService : IHostedService
    {
        private readonly BotConfig _config;
        private readonly ILogger<DiscordClientService> _logger;
        private readonly DiscordSocketClient _client;
        private readonly IServiceProvider _services;

        public DiscordClientService(BotConfig config, ILogger<DiscordClientService> logger, DiscordSocketClient client, IServiceProvider services)
        {
            _client = client;
            _config = config;
            _logger = logger;
            _services = services;

            _client.Log += logMessage => logMessage.Log(_logger);

            RegisterSubsciptions();
        }

        public async Task StartAsync(CancellationToken cToken)
        {
            await _client.LoginAsync(TokenType.Bot, _config.Token);
            await _client.StartAsync();
        }

        public async Task StopAsync(CancellationToken cToken)
        {
            await _client.LogoutAsync();
            await _client.StopAsync();
        }

        #region Subscriptions
        private void RegisterSubsciptions()
        {
            _client.Ready += OnReadyAsync;
            _client.RoleDeleted += OnRoleDeletedAsync;
            _client.GuildMemberUpdated += OnGuildMemberUpdated;
        }

        private async Task OnReadyAsync()
        {
            try
            {
                await _client.SetGameAsync(_config.DefaultStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to ready up client extras");
                //throw;
            }
        }

        private async Task OnRoleDeletedAsync(SocketRole role)
        {
            _logger.LogDebug("Role {role} has been deleted from discord, checking for match in DB", role.GetLogInfo());
            var dbContext = _services.GetRequiredService<DatabaseContext>();
            var dbRole = dbContext.Roles.FirstOrDefault(x => x.RId == role.Id);
            if (dbRole == null)
            {
                _logger.LogDebug("No matching role to delete found for discord role {role}", role.GetLogInfo());
                return;
            }

            _logger.LogInformation("Deleting match {dbRole} for deleted role {role} in DB", dbRole, role.GetLogInfo());
            dbContext.Roles.Remove(dbRole);

            if (await dbContext.SaveChangesAsync() == -1)
                _logger.LogError("Failed to delete match {dbRole} for deleted role {role} in DB", dbRole, role.GetLogInfo());
            else
                _logger.LogInformation("Deleted match {dbRole} for deleted role {role} in DB", dbRole, role.GetLogInfo());
        }

        private async Task OnGuildMemberUpdated(Cacheable<SocketGuildUser, ulong> oldData, SocketGuildUser newUser) //todo: does this trigger if a role gets deleted, if not handle it in deleted
        {
            var oldUser = oldData.Value;
            if (oldUser.Roles.Count <= newUser.Roles.Count)
                return;

            var removedRole = oldUser.Roles.FirstOrDefault(x => !newUser.Roles.Contains(x));
            var dbGuild = await _services.GetRequiredService<DatabaseContext>().GetGuildByIdAsync(newUser.Guild.Id);
            if (dbGuild == null || removedRole == null || dbGuild.CustomColorPermissionRoleId == 0 || removedRole.Id != dbGuild.CustomColorPermissionRoleId)
                return;

            var customRoles = newUser.Roles.Where(x => x.Name.StartsWith("") && x.Name.EndsWith(newUser.Id.ToString())); //todo: fix
            if (!customRoles.Any())
                return;

            var roleList = string.Join(", ", customRoles.Select(x => x.GetLogInfo()));
            try
            {
                _logger.LogInformation("Deleting custom color roles {roles} from guild {guild}, permission role has been removed from owner {user}", roleList, newUser.Guild.GetLogInfo(), newUser.GetLogInfo());
                foreach(var role in customRoles)
                    await role.DeleteAsync();
                _logger.LogInformation("Deleted custom color roles {roles} from guild {guild}, permission role has been removed from owner {user}", roleList, newUser.Guild.GetLogInfo(), newUser.GetLogInfo());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete custom color roles {roles} from guild {guild}, permission role has been removed from owner {user}", roleList, newUser.Guild.GetLogInfo(), newUser.GetLogInfo());
            }
        } //todo: deleteAllCustoms command / when role removed, also delete on user leave
        #endregion
    }
}
