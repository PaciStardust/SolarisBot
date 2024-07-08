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
    public sealed class VouchConfigCommands : SolarisInteractionModuleBase
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
            [Summary(description: "[Opt] Role aquired through vouching (none to disable)")] IRole? vouch = null,
            [Summary(description: "[Opt] Vouch message (\"@person\" will be replaced with ping)")] string message = ""
        )
        {
            var res = await _vouchService.ConfigVouchingAsync(Context.Guild, permission, vouch, message);
            await res.Match(
                success => Interaction.ReplyAsync($"Vouching is currently **{(permission is not null && vouch is not null ? "enabled" : "disabled")}**\n\nPermission: **{permission?.Mention ?? "None"}**\nVouch: **{vouch?.Mention ?? "None"}**\nMessage: **{(string.IsNullOrWhiteSpace(success.Value.VouchMessage) ? "Default" : success.Value.VouchMessage)}**"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

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

        [SlashCommand("info", "View a users vouch info")]
        public async Task GetVouchInfoAsync
        (
            [Summary(description: "Target user")] IUser user,
            [Summary(description: "[Opt] Include missing")] bool missing = false,
            [Summary(description: "[Opt] Limit of vouch entries"), MinValue(1)] int limit = 10
        )
            => await GetInfoAsync(user.Id, missing, limit);

        [SlashCommand("info-id", "View a users vouch history")]
        public async Task GetVouchHistoryCommand
        (
            [Summary(description: "Target user")] string userId,
            [Summary(description: "[Opt] Include missing")] bool missing = false,
            [Summary(description: "[Opt] Limit of vouch entries"), MinValue(1)] int limit = 10
        )
        {
            if (!ulong.TryParse(userId, out var parsedUserId))
            {
                await Interaction.ReplyErrorAsync(StandardError.InvalidParameter("user ID"));
                return;
            }
            await GetInfoAsync(parsedUserId, missing, limit);
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

            foreach (var action in vouchActions)
            {
                sb.Append($"\n:arrow_up: @ <t:{action.VouchedAt}:f>\n<@{action.ExecutingUserId}> *({action.ExecutingUserId})*");
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

        /// <summary>
        /// Responds with info of a specific user
        /// </summary>
        /// <param name="userId">Target user id</param>
        /// <param name="missing">Also add missing users</param>
        /// <param name="limit">Limit of data to fetch</param>
        private async Task GetInfoAsync(ulong userId, bool missing, int limit)
        {
            var res = await _vouchService.GetVouchInfoAsync(Context.Guild, userId, limit, missing);
            await res.Match(
                success => Interaction.ReplyAsync(GenerateVouchInfoEmbed(success.Value.Item1, success.Value.Item2, success.Value.Item3)),
                error => Interaction.ReplyErrorAsync(error.Value)
            );
        }

        /// <summary>
        /// Responds with vouching info of specific user
        /// </summary>
        /// <param name="vouchedBy">Vouched by information</param>
        /// <param name="hasVouched">List of users vouched</param>
        /// <returns>Generated embed</returns>
        private static Embed GenerateVouchInfoEmbed(DbVouchAction? vouchedBy, List<DbVouchAction> hasVouched, int originalCount)
        {
            var embedBuilder = EmbedFactory.Builder();

            if (vouchedBy is not null)
                embedBuilder.AddField("Vouched By", $"<@{vouchedBy.ExecutingUserId}> *({vouchedBy.ExecutingUserId})* @ <t:{vouchedBy.VouchedAt}:f>");

            if (hasVouched.Count != 0)
            {
                var text = string.Join("\n", hasVouched.Select(x => $"<@{x.TargetUserId}> *({x.TargetUserId})* @ <t:{x.VouchedAt}:f>"));
                embedBuilder.AddField($"Has Vouched For ({originalCount} Total)", text);
            }

            return embedBuilder.Build();
        }
        #endregion
    }
}
