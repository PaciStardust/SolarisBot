using Discord;
using Color = Discord.Color;

namespace SolarisBot.Discord.Common
{
    internal static class InteractionExtensions
    {
        /// <summary>
        /// Respond with plaintext
        /// </summary>
        internal static async Task ReplyPlaintextAsync(this IDiscordInteraction interaction, string text, bool isEphemeral = false)
        {
            if (interaction.HasResponded)
                await interaction.FollowupAsync(text, ephemeral: isEphemeral);
            else
                await interaction.RespondAsync(text, ephemeral: isEphemeral);
        }

        /// <summary>
        /// Respond with an embed
        /// </summary>
        internal static async Task ReplyAsync(this IDiscordInteraction interaction, Embed embed, bool isEphemeral = false)
        {
            if (interaction.HasResponded)
                await interaction.FollowupAsync(embed: embed, ephemeral: isEphemeral);
            else
                await interaction.RespondAsync(embed: embed, ephemeral: isEphemeral);
        }

        /// <summary>
        /// Respond with an embed
        /// </summary>
        internal static async Task ReplyAsync(this IDiscordInteraction interaction, string content, Color? colorOverride = null, bool isEphemeral = false)
        {
            var embed = EmbedFactory.Default(content, colorOverride);
            await interaction.ReplyAsync(embed, isEphemeral: isEphemeral);
        }

        /// <summary>
        /// Respond with an embed
        /// </summary>
        internal static async Task ReplyAsync(this IDiscordInteraction interaction, string title, string content, Color? colorOverride = null, bool isEphemeral = false)
        {
            var embed = EmbedFactory.Default(title, content, colorOverride);
            await interaction.ReplyAsync(embed, isEphemeral: isEphemeral);
        }

        /// <summary>
        /// Respond with an error embed
        /// </summary>
        internal static async Task ReplyErrorAsync(this IDiscordInteraction interaction, string content)
        {
            var embed = EmbedFactory.Error(content);
            await interaction.ReplyAsync(embed, true);
        }

        /// <summary>
        /// Respond with an error embed
        /// </summary>
        internal static async Task ReplyErrorAsync(this IDiscordInteraction interaction, string title, string content)
        {
            var embed = EmbedFactory.Error(title, content);
            await interaction.ReplyAsync(embed, true);
        }

        /// <summary>
        /// Respond with an error embed by exception
        /// </summary>
        internal static async Task ReplyErrorAsync(this IDiscordInteraction interaction, Exception exception)
        {
            var embed = EmbedFactory.Error(exception);
            await interaction.ReplyAsync(embed, true);
        }

        /// <summary>
        /// Respond with error embed by GET
        /// </summary>
        internal static async Task ReplyErrorAsync(this IDiscordInteraction interaction, GenericError genericError)
        {
            var embed = EmbedFactory.Error(genericError);
            await interaction.ReplyAsync(embed, true);
        }

        internal static async Task ReplyComponentAsync(this IDiscordInteraction interaction, MessageComponent component, string text = "", bool isEphemeral = false)
        {
            Embed? embed = string.IsNullOrWhiteSpace(text) ? null : EmbedFactory.Default(text);

            if (interaction.HasResponded)
                await interaction.FollowupAsync(embed: embed, components: component, ephemeral: isEphemeral);
            else
                await interaction.RespondAsync(embed: embed, components: component, ephemeral: isEphemeral);
        }

        internal static async Task ReplyAttachmentAsync(this IDiscordInteraction interaction, FileAttachment attachment, bool isEphemeral = false)
        {
            if (interaction.HasResponded)
                await interaction.FollowupWithFileAsync(attachment, ephemeral: isEphemeral);
            else
                await interaction.RespondWithFileAsync(attachment, ephemeral: isEphemeral);
        }
    }
}
