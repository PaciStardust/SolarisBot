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
                await RespondAsync(embed: Embeds.Info("Magic","No Magic has been set for this guild"));
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
                        await RespondAsync(embed: Embeds.Info("Magic", "You have removed your magic"));
                        return;
                    }
                }

                await gUser.AddRoleAsync(guild.MagicRole.Value);
                await RespondAsync(embed: Embeds.Info("Magic", "You have obtained magic"));
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
                await RespondAsync(embed: Embeds.Info("Magic", "No Magic has been set for this guild"));
                return;
            }

            ulong timeNow = DbMain.GetCurrentUnix();
            ulong timeNext = guild.MagicLast + guild.MagicTimeout;
            if (timeNext > timeNow)
            {
                var waitTime = TimeSpan.FromSeconds(timeNext - timeNow);
                await RespondAsync(embed: Embeds.Info("Magic", $"Magic is on cooldown, please wait {waitTime}"));
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
                await RespondAsync(embed: Embeds.Info("Magic", "Something has been changed" + (guild.MagicRename ? $", smells like {role.Name}" : ""), color));
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Embeds.Error(ex));
            }
        }

        [SlashCommand("vouch", "Vouch for a user")]
        public async Task MagicUse(IUser user)
        {
            if (user is not IGuildUser gUser)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var guild = DbGuild.GetOne(Context.Guild.Id) ?? DbGuild.Default;
            if (guild.VouchRole == null)
            {
                await RespondAsync(embed: Embeds.Info("Vouch", "Vouching has not been set up in this guild"));
                return;
            }
            else if (!guild.VouchUser && !gUser.GuildPermissions.Administrator)
            {
                await RespondAsync(embed: Embeds.Info("Vouch", "You do not have permission to vouch"));
                return;
            }
            else if (gUser.RoleIds.Contains(guild.VouchRole.Value))
            {
                await RespondAsync(embed: Embeds.Info("Vouch", "You cannot vouch for someone who already has been vouched for"));
                return;
            }

            try
            {
                await gUser.AddRoleAsync(guild.VouchRole.Value);
                await RespondAsync(embed: Embeds.Info("Vouch", $"You have vouched for {gUser.Mention}"));
            }
            catch (Exception ex)
            {
                await RespondAsync(embed: Embeds.Error(ex));
            }
        }
    }
}
