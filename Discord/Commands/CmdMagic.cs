using Discord.Interactions;
using Discord;
using SolarisBot.Database;
using Microsoft.Data.Sqlite;

namespace SolarisBot.Discord.Commands
{
    [Group("magic", "Magic Role Related Commands")]
    public class CmdMagic : InteractionModuleBase
    {
        [SlashCommand("get","Gives or removes your magic")]
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

        [SlashCommand("use", "Use the power of magic")]
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
                    x.Name = guild.MagicRename ? DbMain.GetName(1, 3) : x.Name;
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
    }
}