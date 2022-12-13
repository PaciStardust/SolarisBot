using Discord.Interactions;
using SolarisBot.Discord;

namespace TesseractBot.Commands
{
    [Group("convert", "Unit conversion")]
    public class CmdConvert : InteractionModuleBase
    {
        #region Temperature Conversion
        public enum Temperature
        {
            Celsius,
            Fahrenheit,
            Kelvin
        }

        private string TempAbbrev(Temperature unit)
            => unit switch
            {
                Temperature.Celsius => "°C",
                Temperature.Fahrenheit => "°F",
                Temperature.Kelvin => "K",
                _ => "?"
            };

        private float TempToC(float value, Temperature unit)
            => unit switch
            {
                Temperature.Celsius => value,
                Temperature.Fahrenheit => (value - 32) * 5f/9f,
                Temperature.Kelvin => value - 273.15f,
                _ => value
            };

        private float TempFromC(float value, Temperature unit)
            => unit switch
            {
                Temperature.Celsius => value,
                Temperature.Fahrenheit => value * 9f/5f + 32,
                Temperature.Kelvin => value + 273.15f,
                _ => value
            };

        private float ConvertTemperature(float v_in, Temperature u_in, Temperature u_out)
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

        private string DistAbbrev(Distance unit)
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

        private Dictionary<Distance, float> ConversionChart = new()
        {
            { Distance.Inch,        0.0254f },
            { Distance.Foot,        0.3048f },
            { Distance.Yard,        0.9144f },
            { Distance.Mile,        1609.344f },
            { Distance.Centimeter,  0.01f },
            { Distance.Meter,       1 },
            { Distance.Kilometer,   1000 }
        };

        private float DistToM(float value, Distance unit)
        {
            if (ConversionChart.ContainsKey(unit)) return value * ConversionChart[unit];
            return value;
        }

        private float DistFromM(float value, Distance unit)
        {
            if (ConversionChart.ContainsKey(unit)) return value * (1 / ConversionChart[unit]);
            return value;
        }

        private float ConvertDistance(float v_in, Distance u_in, Distance u_out)
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
