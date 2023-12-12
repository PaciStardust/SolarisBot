using Discord.Interactions;
using Discord;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using Image = SixLabors.ImageSharp.Image;

namespace SolarisBot.Discord.Modules.Fun
{
    [Module("fun/gifify")]
    internal class GififyCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<GififyCommands> _logger;
        private readonly DatabaseContext _dbContext;
        private readonly HttpClient _httpClient;
        private readonly BotConfig _botConfig;
        internal GififyCommands(ILogger<GififyCommands> logger, DatabaseContext dbctx, HttpClient httpClient, BotConfig botConfig)
        {
            _dbContext = dbctx;
            _logger = logger;
            _httpClient = httpClient;
            _botConfig = botConfig;
        }

        [SlashCommand("cfg-gifify", "[MANAGE MSGS ONLY] Set up gif conversion"), RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task ConfigureGifify
        (
            [Summary(description: "Is feature enabled?")] bool enabled
        )
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.GififyOn = enabled;

            _logger.LogDebug("{intTag} Setting gif conversion to {enabled} in guild {guild}", GetIntTag(), guild.GififyOn, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set gif conversion to {enabled} in guild {guild}", GetIntTag(), guild.GififyOn, Context.Guild.Log());
            await Interaction.ReplyAsync($"Gif conversion is currently **{(guild.GififyOn ? "enabled" : "disabled")}**");
        }

        [MessageCommand("Gifiy")]
        public async Task GififyMessageAsync(IMessage message)
        {
            foreach (var attachment in message.Attachments)
            {
                if (IsValidImage(attachment))
                {
                    await GififyAsync(attachment);
                    return;
                }
            }

            await Interaction.ReplyErrorAsync($"No images below {_botConfig.MaxImageSizeInBytes}MB found in attachments");
            return;
        }

        [SlashCommand("gifify", "Convert image to gif")]
        public async Task GififySlashAsync
        (
            [Summary(description: "Image to convert to gif")] IAttachment image,
            [Summary(description: "[Optional] Only visible locally?")] bool isPrivate = false
        )
        {
            if (!IsValidImage(image))
            {
                await Interaction.ReplyErrorAsync($"Attachment must be an image below {_botConfig.MaxImageSizeInBytes}MB");
                return;
            }

            await GififyAsync(image, isPrivate);
        }

        private async Task GififyAsync(IAttachment image, bool isPrivate = false)
        {
            var guild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);
            if (guild is null || !guild.GififyOn)
            {
                await Interaction.ReplyErrorAsync("Gifify is not enabled in this guild");
                return;
            }

            await Interaction.DeferAsync(isPrivate);

            _logger.LogDebug("{intTag} Converting image {image} to gif - Downloading image", GetIntTag(), image.Url);
            var bytes = await _httpClient.GetByteArrayAsync(image.Url);
            _logger.LogDebug("{intTag} Converting image {image} to gif - Conversion", GetIntTag(), image.Url);
            using var imageStream = new MemoryStream();
            Image.Load(bytes).SaveAsGif(imageStream);
            _logger.LogInformation("{intTag} Converted image {image} to gif", GetIntTag(), image.Url);
            var attachment = new FileAttachment(imageStream, "gifify.gif");
            await Interaction.ReplyAttachmentAsync(attachment, isPrivate);
        }

        private bool IsValidImage(IAttachment attachment)
            => attachment.ContentType.StartsWith("image") && attachment.Size <= 1_000_000 * _botConfig.MaxImageSizeInBytes;
    }
}
