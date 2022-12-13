using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace SolarisBot.ImageGeneration
{
    internal class GradientTextGenerator : GeneratorBase
    {
        //Front text related
        internal string InputText { get; init; } = "Placeholder Text";
        internal string TextFont { get; init; } = "Arial";
        internal int TextSize { get; init; } = 64;
        internal FontStyle TextFontStyle { get; init; } = FontStyle.Regular;
        internal string TextImageLink { get; init; } = "";
        internal GradientMode TextGradientMode { get; init; } = GradientMode.Horizontal;
        internal Color[] TextColors { get; init; } = { Color.White };
        internal TextAlignment TextAlignment { get; init; } = TextAlignment.Start;


        //Comments in code below related to alternate fix to the Gradient drawing issue (Masking)
        internal override async Task<MemoryStream?> GenerateAsync() 
        {
            Logger.Log($"Started creating GradientText Image \"{InputText.Replace("\n", " ")}\"");

            //Grabbing font, if invalid default to arial
            FontFamily fFamily;
            if (TextFont.ToLower() == "random")
            {
                fFamily = SystemFonts.Families.ElementAt(r.Next(SystemFonts.Families.Count()));
            }
            else
            {
                if (!SystemFonts.TryGet(TextFont, out fFamily))
                {
                    fFamily = SystemFonts.Get("Arial");
                }
            }
            var font = fFamily.CreateFont(TextSize, TextFontStyle);
            
            //Setting for text output
            var tOptions = new TextOptions(font)
            {
                TextAlignment = TextAlignment,
                Origin = new Point(0, 0)
            };

            //Measuring text
            var fSize = TextMeasurer.Measure(InputText, tOptions);
            int tWidth = (int)fSize.Width;
            int tHeight = (int)fSize.Height;

            //Create Text Mask
            //var textMask = baseImage.Clone();
            //textMask.Mutate(x => x.DrawText(tOptions, InputText, Color.White));

            //Creating image, width doubled to avoid odd exception when using image or gradientbrush
            var baseImage = new Image<Rgba32>(tWidth * 2, tHeight, Color.Transparent);

            //Creating brush, applying it
            if (TextImageLink != "")
            {
                var textImage = await DownloadImageAsync(TextImageLink);
                textImage ??= new(1, 1, Color.White);

                textImage.Mutate(x => x.Resize(new Size(tWidth, tHeight)));

                var brush = new ImageBrush(textImage);
                baseImage.Mutate(x => x.DrawText(tOptions, InputText, brush));
                textImage.Dispose();
            }
            else
            {
                var brush = CreateLinearGradientBrush(TextGradientMode, TextColors, fSize);
                baseImage.Mutate(x => x.DrawText(tOptions, InputText, brush));
            }

            //Reverting double size fix
            var rOptions = new ResizeOptions()
            {
                Size = new Size(tWidth, tHeight),
                Position = AnchorPositionMode.Left,
                Mode = ResizeMode.Crop
            };
            baseImage.Mutate(x => x.Resize(rOptions));

            //Combining images
            //var gOptions = new GraphicsOptions()
            //{
            //    AlphaCompositionMode = PixelAlphaCompositionMode.DestIn
            //};
            //baseImage.Mutate(x => x.DrawImage(textMask, gOptions));
            //textMask.Dispose();

            Logger.Log($"Finished creating GradientText Image \"{InputText.Replace("\n", " ")}\"");
            return await SaveAndDisposeImageAsync(baseImage);
        }
    }
}
