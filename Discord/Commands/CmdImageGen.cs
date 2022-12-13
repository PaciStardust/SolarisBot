using Discord;
using Discord.Interactions;
using SixLabors.Fonts;
using SolarisBot.Database;
using SolarisBot.Discord;
using SolarisBot.ImageGeneration;

//todo: perm checks

namespace TesseractBot.Commands
{
    [Group("img-gen", "Generate images")]
    public class CmdImageGen : InteractionModuleBase
    {
        private static bool IsImageValid(IAttachment attachment)
        {
            return (attachment.ContentType.StartsWith("image") && attachment.Size <= 8000000);
        }

        [SlashCommand("fonts", "List all fonts")]
        public async Task GenFontlist(string filter = "")
        {
            var fonts = new List<string>();

            foreach (var family in SystemFonts.Families)
            {
                if (family.Name.ToLower().Contains(filter.ToLower()))
                    fonts.Add(family.Name);
            }

            string fontList = string.Join(", ", fonts);
            if (fontList.Length > 1000)
                fontList = fontList[..997] + "...";

            await RespondAsync(embed: Embeds.Info($"List of fonts{(filter == "" ? "" : " (Filtered)")}", fontList));
        }

        [SlashCommand("gen-text", "Generate gradient Text")]
        public async Task GenGradientTextNew(
            [Summary(description: "Text to be displayed")] string text,
            [Summary(description: "Font of text (Def = Random)")] string font = "random",
            [Summary(description: "Style of font (Def = Regular)")] FontStyle fontstyle = FontStyle.Regular,
            [Summary(description: "Colors of text (Def = 2 Random)")] string textcolors = "amazing gradient",
            [Summary(description: "Direction of textgradient (Def = Horizontal)")] GradientMode textgradient = GradientMode.Horizontal,
            [Summary(description: "Alignment of text (Def = Center)")] TextAlignment textalignment = TextAlignment.Center,
            [Summary(description: "Replaces text gradient with image")] IAttachment? textimage = null
        )
        {
            if (Context.User is not IGuildUser gUser)
            {
                await RespondAsync(embed: Embeds.GuildOnly);
                return;
            }

            var guild = DbGuild.GetOne(Context.Guild.Id) ?? DbGuild.Default;

            if (!guild.ImageGen)
            {
                await RespondAsync(embed: Embeds.Info("Image generation disabled", "You are not allowed to generate images in this guild"));
                return;
            }

            if (text.Length > 250 || (textimage != null && !IsImageValid(textimage)))
            {
                await RespondAsync(embed: Embeds.InvalidInput);
                return;
            }

            await DeferAsync();

            var gradGen = new GradientTextGenerator()
            {
                InputText = text.Replace("\\n", "\n"),
                TextFont = font,
                TextFontStyle = fontstyle,
                TextColors = GeneratorBase.GetColorsFromString(textcolors),
                TextGradientMode = textgradient,
                TextAlignment = textalignment,
                TextImageLink = textimage != null ? textimage.Url : ""
            };

            IEnumerable<FileAttachment> enumerable = new List<FileAttachment>() { new FileAttachment(await gradGen.GenerateAsync(), "file.png") };
            await ModifyOriginalResponseAsync(properties => { properties.Attachments = new(enumerable); properties.Content = new($"\"{text.Replace("\\n", " ")}\""); });
        }
    }
}