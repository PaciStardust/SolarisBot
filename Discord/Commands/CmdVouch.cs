using Discord;
using Discord.Interactions;
using Microsoft.Data.Sqlite;
using SolarisBot.Database;

namespace SolarisBot.Discord.Commands
{
    [Group("vouch", "Vouching Commands")]
    internal class CmdVouch : InteractionModuleBase
    {
        [SlashCommand("add", "Vouch for a user")]
        public async Task VouchAdd(IUser user)
        {
            if (user is not IGuildUser gUser || Context.User is not IGuildUser vUser)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var guild = DbGuild.GetOne(Context.Guild.Id) ?? DbGuild.Default;
            if (guild.VouchRole == null)
            {
                await RespondAsync(embed: Embeds.Info("Vouch disabled", "Vouching has not been set up in this guild"));
                return;
            }
            else if (!(vUser.RoleIds.Contains(guild.VouchRole.Value) && guild.VouchUser) && !gUser.GuildPermissions.Administrator)
            {
                await RespondAsync(embed: Embeds.Info("Vouch locked", "You do not have permission to vouch"));
                return;
            }
            else if (gUser.RoleIds.Contains(guild.VouchRole.Value))
            {
                await RespondAsync(embed: Embeds.Info("Vouched already", "You cannot vouch for someone who already has been vouched for"));
                return;
            }

            try
            {
                await gUser.AddRoleAsync(guild.VouchRole.Value);
                await RespondAsync(embed: Embeds.Info("Vouch successful", $"You have vouched for {gUser.Mention}"));
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Embeds.Error(ex));
            }
        }

        [SlashCommand("remove", "[ADMIN ONLY] Remove vouch from a user"), RequireUserPermission(GuildPermission.Administrator)]
        public async Task VouchRemove(IUser user)
        {
            if (user is not IGuildUser gUser)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var guild = DbGuild.GetOne(Context.Guild.Id) ?? DbGuild.Default;
            if (guild.VouchRole == null)
            {
                await RespondAsync(embed: Embeds.Info("Vouch disabled", "Vouching has not been set up in this guild"));
                return;
            }
            else if (!gUser.RoleIds.Contains(guild.VouchRole.Value))
            {
                await RespondAsync(embed: Embeds.Info("Not vouched", "You cannot remove vouch from someone who has not been vouched for"));
                return;
            }

            try
            {
                await gUser.RemoveRoleAsync(guild.VouchRole.Value);
                await RespondAsync(embed: Embeds.Info("Vouch removed", $"You have removed vouch from {gUser.Mention}"));
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Embeds.Error(ex));
            }
        }
    }
}
