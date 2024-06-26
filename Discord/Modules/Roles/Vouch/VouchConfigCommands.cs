using Discord.Interactions;
using Discord;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Common;
using SolarisBot.Database.Models;
using System.Text;

namespace SolarisBot.Discord.Modules.Roles.Vouch
{
    [Module("roles/vouch"), Group("cfg-vouch", "[MANAGE ROLES ONLY] Set up vouching")]
    [RequireContext(ContextType.Guild), DefaultMemberPermissions(GuildPermission.ManageRoles), RequireUserPermission(GuildPermission.ManageRoles)]
    public sealed class VouchConfigCommands : SolarisInteractionModuleBase //todo: [FEATURE] Custom message
    {
        private readonly VouchService _vouchService;

        internal VouchConfigCommands(VouchService vouchService)
        {
            _vouchService = vouchService;
        }

        [SlashCommand("config", "Set up vouching")]
        public async Task ConfigVouchingAsync
        (
            [Summary(description: "[Opt] Role required for vouching (none to disable)")] IRole? permission = null,
            [Summary(description: "[Opt] Role aquired through vouching (none to disable)")] IRole? vouch = null
        )
        {
            var res = await _vouchService.ConfigVouchingAsync(Context.Guild, permission, vouch);
            await res.Match(
                success => Interaction.ReplyAsync($"Vouching is currently **{(permission is not null && vouch is not null ? "enabled" : "disabled")}**\n\nPermission: **{permission?.Mention ?? "None"}**\nVouch: **{vouch?.Mention ?? "None"}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [UserCommand("Vouch History")]
        public async Task GetVouchHistoryUser(IUser user)
            => await GetHistoryAsync(user.Id, 10);

        [SlashCommand("history", "View a users vouch history")]
        public async Task GetVouchHistoryCommand
        (
            [Summary(description: "Target user")] IUser user,
            [Summary(description: "[Opt] Search depth"), MinValue(1)] int depth = 10
        )
            => await GetHistoryAsync(user.Id, depth);

        [SlashCommand("history-id", "View a users vouch history")]
        public async Task GetVouchHistoryCommand
        (
            [Summary(description: "Target user")] string userId,
            [Summary(description: "[Opt] Search depth"), MinValue(1)] int depth = 10
        )
        {
            if (!ulong.TryParse(userId, out var parsedUserId))
            {
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("user ID"));
                return;
            }
            await GetHistoryAsync(parsedUserId, depth);
        }

        #region Utils
        /// <summary>
        /// Generates an embed for a vouch action history
        /// </summary>
        /// <param name="vouchActions">History to create embed for</param>
        /// <returns>Generated embed</returns>
        private static Embed GenerateVouchHistoryEmbed(List<DbVouchAction> vouchActions)
        {
            var sb = new StringBuilder($"<@{vouchActions[0].TargetUserId}> *({vouchActions[0].TargetUserId})*");

            foreach (var action in vouchActions) //todo: [REFACTOR] Check for newline errors on string.join and append
            {
                sb.Append($"\n:arrow_up:\n<@{action.ExecutingUserId}> *({action.ExecutingUserId})*");
            }

            return EmbedFactory.Default("Vouch History", sb.ToString());
        }

        /// <summary>
        /// Responds with vouching history of specified user
        /// </summary>
        /// <param name="userId">Target user id</param>
        /// <param name="maxDepth">Maximum search depth</param>
        private async Task GetHistoryAsync(ulong userId, int maxDepth)
        {
            var res = await _vouchService.GetVouchHistoryAsync(Context.Guild, userId, maxDepth);
            await res.Match(
                success => Interaction.ReplyAsync(GenerateVouchHistoryEmbed(success.Value)),
                error => Interaction.ReplyErrorAsync(error.Value)
            );
        }
        #endregion
    }
}
