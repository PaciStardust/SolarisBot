using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Modules.Owner
{
    [Module("owner"), Group("owner", "[OWNER ONLY] Configure Solaris"), DefaultMemberPermissions(GuildPermission.Administrator), RequireOwner]
    public sealed class OwnerCommands : SolarisInteractionModuleBase
    {
        private readonly OwnerService _ownerService;

        internal OwnerCommands(OwnerService ownerService)
        {
            _ownerService = ownerService;
        }

        [SlashCommand("set-status", "Set the status of the bot")]
        public async Task SetStatusAsync
        (
            [Summary(description: "New bot status")] string status
        )
        {
            var res = await _ownerService.SetStatusAsync(status);
            await res.Match(
                success => Interaction.ReplyAsync($"Status set to \"{status}\""),
                error => Interaction.ReplyErrorAsync(error.Value),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }

        [SlashCommand("stats", "List runtime and command count")]
        public async Task StatsAsync()
        {
            var statsString = _ownerService.GetStatsString();
            await Interaction.ReplyAsync("Statistics", statsString);
        }

        [SlashCommand("sql-run", "Run SQL")]
        public async Task SqlRunAsync(string query)
        {
            var res = await _ownerService.RunRawSqlAsync(query);
            await res.Match(
                success => Interaction.ReplyAsync($"Ran raw SQL: {query}\n\n{success.Value} line(s) affected"),
                exception => Interaction.ReplyErrorAsync(exception.Value)
            );
        }
    }
}
