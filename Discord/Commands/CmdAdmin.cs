using Discord.Interactions;
using Discord;
using SolarisBot.Database;
using SolarisBot.Discord;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;

namespace SolarisBot.Discord.Commands
{
    [Group("admin", "[ADMIN ONLY] Server Admin Commands"), RequireUserPermission(GuildPermission.Administrator)]
    public class CmdAdmin : InteractionModuleBase
    {
        [UserCommand("user-info"), SlashCommand("user-info", "Get all info about a user")]
        public async Task UserInfo(IGuildUser user)
        {
            if (Context.Guild == null)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var name = $"{user.Username}#{user.DiscriminatorValue}";
            if (user.Nickname == user.Username)
                name = $"{user.Nickname} ({name})";

            var unixCreated = user.CreatedAt.ToUnixTimeSeconds();
            var unixJoined = user.JoinedAt?.ToUnixTimeSeconds() ?? 0;

            var userBot = user.IsWebhook ? "Webhook"
                : user.IsBot ? "Bot"
                : "User";

            var status = user.Status.ToString();
            if (string.IsNullOrWhiteSpace(status))
                status = "*No status set*";

            var infoEmbed = new EmbedBuilder()
            {
                Title = $"Details > {user.Mention}",
                Color = Embeds.ColorDefault,
                ThumbnailUrl = user.GetDisplayAvatarUrl()
            }
            .AddField("Name", name)
            .AddField("Status", status)
            .AddField("Created At", $"<t:{unixCreated}:f> (<t:{unixCreated}:R>)")
            .AddField("Joined At", $"<t:{unixJoined}:f> (<t:{unixJoined}:R>)")
            .AddField("Is Bot?", userBot)
            .AddField("Admin?", user.GuildPermissions.Administrator ? "Yes" : "No", true)
            .AddField("Id", user.Id, true);

            var userActivities = user.Activities;
            if (userActivities.Count > 0)
                infoEmbed.AddField("Activity", userActivities.ToArray()[0].Name);

            var unixBoost = user.PremiumSince?.ToUnixTimeSeconds() ?? 0;
            if (unixBoost > 0)
                infoEmbed.AddField("Boosting Since", $"<t:{unixBoost}:f> (<t:{unixBoost}:R>)");

            var unixTimeout = user.TimedOutUntil?.ToUnixTimeSeconds() ?? 0;
            if (unixTimeout > 0)
                infoEmbed.AddField("Timed out until", $"<t:{unixBoost}:f> (<t:{unixBoost}:R>)");

            var userVoice = user.VoiceChannel?.Name;
            if (!string.IsNullOrWhiteSpace(userVoice))
            {
                var userMute = user.IsMuted ? "Guild"
                    : user.IsSelfMuted ? "Self"
                    : "No";
                var userDeaf = user.IsDeafened ? "Guild"
                    : user.IsSelfDeafened ? "Self"
                    : "No";

                string? userCallActivity = null;
                if (user.IsStreaming)
                    userCallActivity = "Stream";
                if (user.IsVideoing)
                {
                    if (userCallActivity == null)
                        userCallActivity = "Video";
                    else
                        userCallActivity += " + Video";
                }

                infoEmbed.AddField("Voice", userVoice)
                    .AddField("Muted?", userMute)
                    .AddField("Deafened?", userDeaf, true)
                    .AddField("Video?", userCallActivity ?? "No", true);
            }

            await RespondAsync(embed: infoEmbed.Build());
        }

        [SlashCommand("renaming-enable", "Allow renaming on \"I am\" messages, off by default")]
        public async Task RenamingEnable(bool enabled)
        {
            if (Context.Guild == null)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var parameters = new SqliteParameter[]
            {
                new SqliteParameter("RENAMING", enabled),
                new("ID", Context.Guild.Id),
            };

            if (DbMain.Run("UPDATE guildSettings SET renaming = @RENAMING where id = @ID", true, parameters) < 1)
            {
                var guild = new DbGuild()
                {
                    Id = Context.Guild.Id,
                    Renaming = enabled
                };

                if (!guild.Create())
                {
                    await RespondAsync(embed: Embeds.DbFailure);
                    return;
                }
            }

            await RespondAsync(embed: Embeds.Info("Renaming", "Renaming has been " + (enabled ? "enabled" : "disabled")));
        }

        [SlashCommand("magic-configure", "[ADMIN ONLY] Configures the magic role")]
        public async Task MagicSet(IRole? role = null, ushort timeout = 1800, bool rename = false)
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

            if (DbMain.Run("UPDATE guildSettings SET magicrole = @MAGICROLE, magictimeout = @MAGICTIMEOUT, magicrename = @MAGICRENAME, magiclast = 0 where id = @ID", true, parameter) < 1)
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

            await RespondAsync(embed: Embeds.Info("Magic", "Magic has been configured"));
        }
    }
}
