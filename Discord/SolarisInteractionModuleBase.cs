using Discord;
using Discord.Interactions;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Discord
{
    /// <summary>
    /// Extended InteractionModeuleBase with a few core functions
    /// </summary>
    public abstract class SolarisInteractionModuleBase : InteractionModuleBase
    {
        /// <summary>
        /// Needs to be overriden for internal error logging
        /// </summary>
        protected virtual ILogger? GetLogger() => null;

        /// <summary>
        /// Gets an interaction tag for logging
        /// </summary>
        protected string GetIntTag() => $"[Int {Context.Interaction.Id}]";

        #region Embeds
        /// <summary>
        /// Respond with an embed
        /// </summary>
        internal async Task RespondEmbedAsync(Embed embed, bool isEphemeral = false)
        {
            try
            {
                await RespondAsync(embed: embed, ephemeral: isEphemeral);
            }
            catch (Exception ex)
            {
                GetLogger()?.LogError(ex, "Failed to respond to interaction");
            }
        }

        /// <summary>
        /// Respond with an embed
        /// </summary>
        internal async Task RespondEmbedAsync(string title, string content, Color? colorOverride = null, bool isEphemeral = false)
        {
            var embed = DiscordUtils.Embed(title, content, colorOverride);
            await RespondEmbedAsync(embed, isEphemeral: isEphemeral);
        }

        /// <summary>
        /// Respond with an error embed
        /// </summary>
        internal async Task RespondErrorEmbedAsync(string title, string content)
        {
            var embed = DiscordUtils.EmbedError(title, content);
            await RespondEmbedAsync(embed, true);
        }

        /// <summary>
        /// Respond with an error embed by exception
        /// </summary>
        internal async Task RespondErrorEmbedAsync(Exception exception)
        {
            var embed = DiscordUtils.EmbedError(exception);
            await RespondEmbedAsync(embed, true);
        }

        /// <summary>
        /// Respond with error embed by GET
        /// </summary>
        internal async Task RespondErrorEmbedAsync(EmbedGenericErrorType get)
        {
            var embed = DiscordUtils.EmbedError(get);
            await RespondEmbedAsync(embed, true);
        }

        /// <summary>
        /// Respond with an invalid input error
        /// </summary>
        internal async Task RespondInvalidInputErrorEmbedAsync(string reason)
        {
            var embed = DiscordUtils.EmbedError("Invalid Input", reason);
            await RespondEmbedAsync(embed, true);
        }
        #endregion
    }
}
