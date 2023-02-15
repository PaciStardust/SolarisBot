using Discord;
using Discord.Interactions;
using Microsoft.Data.Sqlite;
using SolarisBot.Database;
using SolarisBot;
using SolarisBot.Discord;
using System.Reflection.Metadata;

namespace TesseractBot.Commands
{
    [Group("config", "Configure Bot")]
    public class CmdConfig : InteractionModuleBase
    {
        [SlashCommand("magic", "[ADMIN ONLY] Configures the magic role")]
        public async Task ConfigMagic(IRole? role = null, ushort timeout = 1800, bool rename = false)
        {
            if (Context.Guild == null)
            {
                await ReplyAsync(embed: Embeds.GuildOnly);
                return;
            }

            var roleId = role?.Id;

            var parameter = new SqliteParameter[]
            {
                new("MAGICROLE", roleId),
                new("MAGICTIMEOUT", timeout),
                new("MAGICRENAME", rename),
                new("ID", Context.Guild.Id)
            };

            if (DbMain.Run("UPDATE guilds SET magicrole = @MAGICROLE, magictimeout = @MAGICTIMEOUT, magicrename = @MAGICRENAME, magiclast = 0 where id = @ID", true, parameter) < 1)
            {
                var guild = new DbGuild()
                {
                    Id = Context.Guild.Id,
                    MagicRole = roleId,
                    MagicRename = rename,
                    MagicTimeout = timeout
                };

                if (!guild.Create())
                {
                    await RespondAsync(embed: Embeds.DbFailure);
                    return;
                }
            }

            Logger.Log($"Magic has been configured ({DbMain.SummarizeSqlParameters(parameter)})");
            await RespondAsync(embed: Embeds.Info("Magic configured", "Magic has been configured"));
        }

        [SlashCommand("vouch", "[ADMIN ONLY] Configures the vouch command")]
        public async Task ConfigVouch(IRole? role = null, bool uservouch = false)
        {
            if (Context.Guild == null)
            {
                await ReplyAsync(embed: Embeds.GuildOnly);
                return;
            }

            var roleId = role?.Id;

            var parameter = new SqliteParameter[]
            {
                new("VOUCHROLE", roleId),
                new("VOUCHUSER", uservouch),
                new("ID", Context.Guild.Id)
            };

            if (DbMain.Run("UPDATE guilds SET vouchrole = @VOUCHROLE, vouchuser = @VOUCHUSER, magiclast = 0 where id = @ID", true, parameter) < 1)
            {
                var guild = new DbGuild()
                {
                    Id = Context.Guild.Id,
                    VouchRole = roleId,
                    VouchUser = uservouch
                };

                if (!guild.Create())
                {
                    await RespondAsync(embed: Embeds.DbFailure);
                    return;
                }
            }

            Logger.Log($"Vouch has been configured ({DbMain.SummarizeSqlParameters(parameter)})");
            await RespondAsync(embed: Embeds.Info("Vouch configured", "Vouch has been configured"));
        }

        [SlashCommand("vcgen", "[ADMIN ONLY] Configures the vcgen system")] //todo: test
        public async Task ConfigureVcGen(IVoiceChannel? channel = null, ushort maxchannels = 4)
        {
            if (Context.Guild == null)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var parameters = new SqliteParameter[]
            {
                new("VCGENCHANNEL", channel?.Id),
                new("VCGENMAX", maxchannels),
                new("ID", Context.Guild.Id)
            };

            if (DbMain.Run("UPDATE guilds SET vcgenchannel = @VCGENCHANNEL, vcgenmax = @VCGENMAX where id = @ID", true, parameters) < 1)
            {
                var guild = new DbGuild()
                {
                    Id = Context.Guild.Id,
                    VcGenChannel = channel?.Id,
                    VcGenMax = maxchannels
                };

                if (!guild.Create())
                {
                    await RespondAsync(embed: Embeds.DbFailure);
                    return;
                }
            }

            Logger.Log($"DbGen has been configured in {Context.Guild.Id}");
            await RespondAsync(embed: Embeds.Info("VcGen configured", "VcGen has been configured"));
        }

        [SlashCommand("other", "Toggle other bot features in this guild")]
        public async Task ConfigOther(bool renaming = false, bool imagegen = false)
        {
            if (Context.Guild == null)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var parameters = new SqliteParameter[]
            {
                new("RENAMING", renaming),
                new("IMAGEGEN", imagegen),
                new("ID", Context.Guild.Id),
            };

            if (DbMain.Run("UPDATE guilds SET renaming = @RENAMING, imagegen = @IMAGEGEN WHERE id = @ID", true, parameters) < 1)
            {
                var guild = new DbGuild()
                {
                    Id = Context.Guild.Id,
                    Renaming = renaming,
                    ImageGen = imagegen,
                };

                if (!guild.Create())
                {
                    await RespondAsync(embed: Embeds.DbFailure);
                    return;
                }
            }

            Logger.Log($"Features have been changed in {Context.Guild.Id}");
            await RespondAsync(embed: Embeds.Info("Features configured", "Features have successfully been configured"));
        }
    }
}