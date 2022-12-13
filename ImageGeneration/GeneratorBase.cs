using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SolarisBot;
using System.Net;
using System.Text.RegularExpressions;

namespace SolarisBot.ImageGeneration
{
    internal abstract class GeneratorBase
    {
        internal static Random r = new();
        private static readonly Regex RGBColorRegEX = new("^\\d{1,3},\\d{1,3},\\d{1,3}$");

        internal abstract Task<MemoryStream> GenerateAsync();

        #region Color Related
        internal static Color GetRandomColor()
        {
            var bytes = new byte[3];
            r.NextBytes(bytes);
            return Color.FromRgb(bytes[0], bytes[1], bytes[2]);
        }

        internal static Color GetColor(string input)
        {

            if (RGBColorRegEX.IsMatch(input))
            {
                string[] splitColors = input.Split(',');
                byte[] colorsParsed = new byte[3];

                try
                {
                    for (int a = 0; a < 3; a++)
                    {
                        colorsParsed[a] = Convert.ToByte(splitColors[a]);
                    }
                    return Color.FromRgb(colorsParsed[0], colorsParsed[1], colorsParsed[2]);
                }
                catch { }
            }

            if (Color.TryParse(input, out var color))
                return color;

            return GetRandomColor();
        }

        internal static Color[] GetColorsFromString(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return Array.Empty<Color>();

            string[] inputParsed = input.Split(" ");

            Color[] colors = new Color[inputParsed.Length];

            for (int i = 0; i < inputParsed.Length; i++)
            {
                colors[i] = GetColor(inputParsed[i]);
            }

            return colors;
        }
        #endregion

        #region File Creation
        internal static async Task<MemoryStream> SaveAndDisposeImageAsync(Image image)
        {
            //Saving image
            var ms = new MemoryStream();
            await image.SaveAsPngAsync(ms);
            image.Dispose();
            return ms;
        }

        internal static async Task<Image<Rgba32>?> DownloadImageAsync(string url)
        {
            try
            {
                var client = new HttpClient();
                var result = await client.GetStreamAsync(url);
                Logger.Info("Successfully downloaded image at " + url, "ImgGen");
                return await Image.LoadAsync<Rgba32>(result);
            }
            catch
            {
                Logger.Info("Failed to download image at " + url, "ImgGen");
                return null;
            }
        }
        #endregion

        #region Brush Creation
        internal static LinearGradientBrush CreateLinearGradientBrush(GradientMode gradientMode, Color[] colors, FontRectangle fSize)
        {
            //Determining start and stop positions of gradient
            PointF posTL = gradientMode switch
            {
                GradientMode.DiagonalFlipped => new(0, fSize.Height),
                _ => new(0, 0)
            };
            PointF posBR = gradientMode switch
            {
                GradientMode.Diagonal => new(fSize.Width, fSize.Height),
                GradientMode.Vertical => new(0, fSize.Height),
                _ => new(fSize.Width, 0)
            };

            //Calculating ColorStop array
            int colLen = colors.Length;
            var colorStop = new ColorStop[colLen];

            if (colLen > 1)
            {
                float ratio = 1f / (colLen - 1);
                for (int i = 0; i < colLen; i++)
                {
                    colorStop[i] = new(i * ratio, colors[i]);
                }
            }
            else
            {
                //If there is an element, supply it, else supply random
                colorStop = new ColorStop[] { new(0, colLen != 0 ? colors[0] : GetRandomColor()) };
            }
            
            return new LinearGradientBrush(posTL, posBR, GradientRepetitionMode.DontFill, colorStop);
        }
        #endregion
    }

    public enum GradientMode
    {
        Vertical,
        Horizontal,
        Diagonal,
        DiagonalFlipped
    }
}
