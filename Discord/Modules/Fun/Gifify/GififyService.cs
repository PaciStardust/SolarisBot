using Discord;
using Microsoft.Extensions.Logging;
using OneOf;
using OneOf.Types;
using SixLabors.ImageSharp;
using SolarisBot.Database;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using Image = SixLabors.ImageSharp.Image;

namespace SolarisBot.Discord.Modules.Fun.Gifify
{
    [Module("fun/gifify"), AutoLoadService]
    internal class GififyService
    {
        private readonly ILogger<GififyService> _logger;
        private readonly DatabaseService _dbService;
        private readonly HttpClient _httpClient;
        private readonly BotConfig _botConfig;

        public GififyService(ILogger<GififyService> logger, DatabaseService dbService, HttpClient httpClient, BotConfig botConfig)
        {
            _dbService = dbService;
            _logger = logger;
            _httpClient = httpClient;
            _botConfig = botConfig;
        }

        /// <summary>
        /// Enables or disables feature in a guild
        /// </summary>
        /// <param name="guild">Guild to configure</param>
        /// <param name="enabled">Feature enabled?</param>
        /// <returns>Successs / Exception</returns>
        internal async Task<OneOf<Success<DbGuildConfig>, Error<Exception>>> ConfigureGififyAsync(IGuild guild, bool enabled)
        {
            using var dbCtx = _dbService.GetContext();

            var dbGuild = await dbCtx.GetOrCreateTrackedGuildAsync(guild.Id);
            dbGuild.GififyOn = enabled;

            _logger.LogDebug("Setting gif conversion to {enabled} in guild {guild}", dbGuild.GififyOn, guild.Log());
            var (_, err) = await dbCtx.TrySaveChangesAsync();
            if (err is not null)
            {
                _logger.LogError(err, "Failed setting gif conversion to {enabled} in guild {guild}", dbGuild.GififyOn, guild.Log());
                return new Error<Exception>(err);
            }
            _logger.LogInformation("Setting gif conversion to {enabled} in guild {guild}", dbGuild.GififyOn, guild.Log());
            return new Success<DbGuildConfig>(dbGuild);
        }

        /// <summary>
        /// Turns attachment to gif
        /// </summary>
        /// <param name="guild">Origin guild</param>
        /// <param name="image">Image to convert</param>
        /// <returns>Success / Error as string</returns>
        internal async Task<OneOf<Success<FileAttachment>, Error<string>>> GififyAsync(IGuild guild, IAttachment image)
        {
            if (!IsValidImage(image))
                return new Error<string>($"Attachment must be an image below {_botConfig.MaxImageSizeInBytes}MB");

            using var dbCtx = _dbService.GetContext();

            var dbGuild = await dbCtx.GetGuildByIdAsync(guild.Id);
            if (dbGuild is null || !dbGuild.GififyOn)
                return new Error<string>("Gifify is not enabled in this guild");

            _logger.LogDebug("Converting image {image} to gif for guild {guild} - Downloading image", guild.Log(), image.Url);
            var bytes = await _httpClient.GetByteArrayAsync(image.Url);
            _logger.LogDebug("Converting image {image} to gif for guild {guild} - Conversion", guild.Log(), image.Url);
            var imageStream = new MemoryStream();
            Image.Load(bytes).SaveAsGif(imageStream);
            _logger.LogInformation("Converted image {image} for guild {guild} to gif", guild.Log(), image.Url);
            var attachment = new FileAttachment(imageStream, "gifify.gif");
            return new Success<FileAttachment>(attachment);
        }

        /// <summary>
        /// Checks validity of image
        /// </summary>
        /// <param name="attachment">Attachment to check</param>
        /// <returns>Valid?</returns>
        private bool IsValidImage(IAttachment attachment)
            => attachment.ContentType.StartsWith("image") && attachment.Size <= 1_000_000 * _botConfig.MaxImageSizeInBytes;
    }
}
