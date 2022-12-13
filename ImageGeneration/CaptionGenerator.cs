using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.ColorSpaces;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Processing.Processors.Transforms;

namespace SolarisBot.ImageGeneration
{
    internal class CaptionGenerator : GeneratorBase
    {
        //Front text related
        internal string InputText { get; init; } = "Placeholder Text";
        internal string TextImageLink { get; init; } = "";
        internal bool NoImpact { get; init; } = false;

        //Comments in code below related to alternate fix to the Gradient drawing issue (Masking)
        internal override async Task<MemoryStream?> GenerateAsync() 
        {
            Logger.Log($"Started creating Captioned Image \"{InputText.Replace("\n", " ")}\"");

            var fFamily = SystemFonts.Get((NoImpact ? "Arial" : "Impact"));
            var font = fFamily.CreateFont(64, FontStyle.Regular);
            
            //Setting for text output
            var tOptions = new TextOptions(font)
            {
                TextAlignment = TextAlignment.Center,
                Origin = new Point(8, 8)
            };

            //Measuring text
            var fSize = TextMeasurer.Measure(InputText, tOptions);
            int tWidth = (int)fSize.Width;
            int tHeight = (int)fSize.Height;

            //Creating and writing on textImage
            var textImage = new Image<Rgba32>(tWidth + 16, tHeight + 16, Color.White);
            var brush = new SolidBrush(Color.Black);
            textImage.Mutate(x => x.DrawText(tOptions, InputText, brush));

            //Downloading of image
            var downImage = await DownloadImageAsync(TextImageLink);
            if (downImage == null)
                return null;

            //Scaling text to match width
            var rOpt = new ResizeOptions()
            {
                Sampler = new NearestNeighborResampler(),
                Size = new(downImage.Width, tHeight * downImage.Width / textImage.Width)
            };
            textImage.Mutate(x => x.Resize(rOpt));

            //Creation of final image
            var finalImage = new Image<Rgba32>(downImage.Width, downImage.Height + textImage.Height);
            finalImage.Mutate(x => x.DrawImage(textImage, new Point(0, 0), 1));
            finalImage.Mutate(x => x.DrawImage(downImage, new Point(0, textImage.Height), 1));
            textImage.Dispose();
            downImage.Dispose();

            Logger.Log($"Finished creating Captioned Image \"{InputText.Replace("\n", " ")}\"");
            return await SaveAndDisposeImageAsync(finalImage);
        }
    }
}
