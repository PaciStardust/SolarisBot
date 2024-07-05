using Discord;
using Color = Discord.Color;

namespace SolarisBot.Discord.Common
{
    internal static class EmbedFactory
    {
        #region Default
        /// <summary>
        /// Generates a default embedbuilder
        /// </summary>
        internal static EmbedBuilder Builder(Color? colorOverride = null)
            => new()
            {
                Color = colorOverride ?? Color.Blue
            };

        /// <summary>
        /// Generates a default embed
        /// </summary>
        internal static Embed Default(string content, Color? colorOverride = null)
            => Builder(colorOverride)
                .WithDescription(content)
                .Build();

        /// <summary>
        /// Generates a default embed with title
        /// </summary>
        internal static Embed Default(string title, string content, Color? colorOverride = null)
            => Builder(colorOverride)
                .WithTitle(title)
                .WithDescription(content)
                .Build();
        #endregion

        #region Errors
        /// <summary>
        /// Generates a error embedbuilder
        /// </summary>
        internal static EmbedBuilder ErrorBuilder()
            => Builder(Color.Red);

        /// <summary>
        /// Generates an embed error
        /// </summary>
        internal static Embed Error(string message)
            => ErrorBuilder()
                .WithDescription(message)
                .Build();

        /// <summary>
        /// Generates an embed error with title
        /// </summary>
        internal static Embed Error(string title, string content)
            => ErrorBuilder()
                .WithTitle(title)
                .WithDescription(content)
                .Build();

        /// <summary>
        /// Generates an embed error based on an exception
        /// </summary>
        internal static Embed Error(Exception exception)
            => Error(exception.GetType().Name, exception.Message);

        /// <summary>
        /// Generates an embed error for DMs with the "Can be disabled" tooltip
        /// </summary>
        internal static Embed SystemError(string title, string content)
            => ErrorBuilder()
                .WithTitle(title)
                .WithDescription(content)
                .WithFooter("DM reports like this can be disabled with /cfg-errordm")
                .Build();
        #endregion
    }

    internal enum GenericError
    {
        NoResults
    }
}
