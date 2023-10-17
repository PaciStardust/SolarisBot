using Discord;

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
        /// Generates a generic embed error based on GenericError
        /// </summary>
        internal static Embed Error(GenericError genericError)
        {
            var data = genericError switch
            {
                GenericError.NoResults => "Request yielded no results",
                _ => "An unknown error occured"
            };
            return Error(data);
        }
        #endregion
    }

    internal enum GenericError
    {
        NoResults
    }
}
