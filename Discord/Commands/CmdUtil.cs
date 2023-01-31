using Discord.Interactions;
using Discord;
using SolarisBot.Database;
using SolarisBot.Discord;
using Microsoft.Data.Sqlite;
using System.Runtime.InteropServices;
using System.Reflection.Metadata;

namespace SolarisBot.Discord.Commands
{
    [Group("util", "Utility Commands")]
    public class CmdUtil : InteractionModuleBase
    {
        #region Other
        [UserCommand("userinfo"), SlashCommand("userinfo", "Get all info about a user")]
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
        #endregion

        #region Temperature Conversion
        public enum Temperature
        {
            Celsius,
            Fahrenheit,
            Kelvin
        }

        private static string TempAbbrev(Temperature unit)
            => unit switch
            {
                Temperature.Celsius => "°C",
                Temperature.Fahrenheit => "°F",
                Temperature.Kelvin => "K",
                _ => "?"
            };

        private static float TempToC(float value, Temperature unit)
            => unit switch
            {
                Temperature.Celsius => value,
                Temperature.Fahrenheit => (value - 32) * 5f / 9f,
                Temperature.Kelvin => value - 273.15f,
                _ => value
            };

        private static float TempFromC(float value, Temperature unit)
            => unit switch
            {
                Temperature.Celsius => value,
                Temperature.Fahrenheit => value * 9f / 5f + 32,
                Temperature.Kelvin => value + 273.15f,
                _ => value
            };

        private static float ConvertTemperature(float v_in, Temperature u_in, Temperature u_out)
            => TempFromC(TempToC(v_in, u_in), u_out);
        #endregion

        #region Distance Conversion
        public enum Distance
        {
            Inch,
            Foot,
            Yard,
            Mile,
            Centimeter,
            Meter,
            Kilometer
        }

        private static string DistAbbrev(Distance unit)
            => unit switch
            {
                Distance.Inch => "in",
                Distance.Foot => "ft",
                Distance.Yard => "yd",
                Distance.Mile => "mi",
                Distance.Centimeter => "cm",
                Distance.Meter => "m",
                Distance.Kilometer => "km",
                _ => "?"
            };

        private static readonly Dictionary<Distance, float> ConversionChart = new()
        {
            { Distance.Inch,        0.0254f },
            { Distance.Foot,        0.3048f },
            { Distance.Yard,        0.9144f },
            { Distance.Mile,        1609.344f },
            { Distance.Centimeter,  0.01f },
            { Distance.Meter,       1 },
            { Distance.Kilometer,   1000 }
        };

        private static float DistToM(float value, Distance unit)
        {
            if (ConversionChart.TryGetValue(unit, out float val)) return value * val;
            return value;
        }

        private static float DistFromM(float value, Distance unit)
        {
            if (ConversionChart.TryGetValue(unit, out float val)) return value * (1 / val);
            return value;
        }

        private static float ConvertDistance(float v_in, Distance u_in, Distance u_out)
            => DistFromM(DistToM(v_in, u_in), u_out);
        #endregion

        #region Commands
        [SlashCommand("temperature", "Convert temperature")]
        public async Task CommandConvertTemperature(float v_in, Temperature u_in, Temperature u_out)
        {
            var v_out = ConvertTemperature(v_in, u_in, u_out);
            await RespondAsync(embed: Embeds.Info($"{u_in} to {u_out}", $"{v_in}{TempAbbrev(u_in)} => {v_out}{TempAbbrev(u_out)}"));
        }

        [SlashCommand("distance", "Convert distance")]
        public async Task CommandConvertDistance(float v_in, Distance u_in, Distance u_out)
        {
            var v_out = ConvertDistance(v_in, u_in, u_out);
            await RespondAsync(embed: Embeds.Info($"{u_in} to {u_out}", $"{v_in}{DistAbbrev(u_in)} => {v_out}{DistAbbrev(u_out)}"));
        }

        [SlashCommand("height-m-ft", "Convert height in meters to height in feet")]
        public async Task CommandConvertHeightToFeet(float meters)
        {
            int feet = (int)DistFromM(meters, Distance.Foot);
            float inches = DistFromM(meters - DistToM(feet, Distance.Foot), Distance.Inch);

            await RespondAsync(embed: Embeds.Info("Meters to Foot + Inch", $"{meters}{DistAbbrev(Distance.Meter)} => {feet}{DistAbbrev(Distance.Foot)} {inches}{DistAbbrev(Distance.Inch)}"));
        }

        [SlashCommand("height-ft-m", "Converts height in feet and inches to meters")]
        public async Task CommandConvertHeightToMeters(int feet, float inches)
        {
            float meters = DistToM(feet, Distance.Foot) + DistToM(inches, Distance.Inch);
            await RespondAsync(embed: Embeds.Info("Foot + Inch to Meters", $"{feet}{DistAbbrev(Distance.Foot)} {inches}{DistAbbrev(Distance.Inch)} => {meters}{DistAbbrev(Distance.Meter)}"));
        }
        #endregion
    }
}
