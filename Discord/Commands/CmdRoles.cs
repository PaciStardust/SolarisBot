using Discord.Interactions;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SolarisBot.Database;
using Microsoft.Data.Sqlite;

namespace SolarisBot.Discord.Commands
{
    [Group("roles", "Role Related Commands")]
    public class CmdRoles : InteractionModuleBase
    {
        [SlashCommand("magic-get","Gives you magic")]
        public async Task MagicGet()
        {
            if (Context.User is not IGuildUser gUser)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var guild = DbGuild.GetOne(Context.Guild.Id) ?? DbGuild.Default;

            if (guild.MagicRole == null)
            {
                await RespondAsync(embed: Embeds.Info("Magic disabled","No Magic has been set for this guild"));
                return;
            }

            try
            {
                var roles = gUser.RoleIds;
                foreach (var role in roles)
                {
                    if (role == guild.MagicRole)
                    {
                        await gUser.RemoveRoleAsync(guild.MagicRole.Value);
                        await RespondAsync(embed: Embeds.Info("Magic lost", "You have removed your magic"));
                        return;
                    }
                }

                await gUser.AddRoleAsync(guild.MagicRole.Value);
                await RespondAsync(embed: Embeds.Info("Magic obtained", "You have obtained magic"));
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Embeds.Error(ex));
            }
        }

        [SlashCommand("magic-use", "Use the power of magic")]
        public async Task MagicUse()
        {
            if (Context.Guild == null)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var guild = DbGuild.GetOne(Context.Guild.Id) ?? DbGuild.Default;
            if (guild.MagicRole == null)
            {
                await RespondAsync(embed: Embeds.Info("Magic disabled", "No Magic has been set for this guild"));
                return;
            }

            ulong timeNow = DbMain.GetCurrentUnix();
            ulong timeNext = guild.MagicLast + guild.MagicTimeout;
            if (timeNext > timeNow)
            {
                var waitTime = TimeSpan.FromSeconds(timeNext - timeNow);
                await RespondAsync(embed: Embeds.Info("Magic on cooldown", $"Magic is on cooldown, please wait {waitTime}"));
                return;
            }

            try
            {
                var r = DbMain.Random;

                var role = Context.Guild.GetRole(guild.MagicRole.Value);
                var color = new Color(r.Next(255), r.Next(255), r.Next(255));

                await role.ModifyAsync(x =>
                {
                    x.Color = new(color);
                    x.Name = guild.MagicRename ? DbMain.GetName(3, 5) : x.Name;
                });

                if (!DbGuild.SetOne("magiclast", timeNow, guild.Id))
                {
                    await RespondAsync(embed: Embeds.DbFailure);
                    return;
                }

                Logger.Log($"Magic has been used in {Context.Guild.Id}");
                await RespondAsync(embed: Embeds.Info("Magic used", "Something has been changed", color));
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Embeds.Error(ex));
            }
        }

        [SlashCommand("vouch", "Vouch for a user")]
        public async Task MagicUse(IUser user)
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
                await RespondAsync(embed: Embeds.Info("Vouch Successful", $"You have vouched for {gUser.Mention}"));
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Embeds.Error(ex));
            }
        }
    }
}
