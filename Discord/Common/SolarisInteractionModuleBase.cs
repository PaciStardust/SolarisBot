using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace SolarisBot.Discord.Common
{
    /// <summary>
    /// Extended InteractionModeuleBase with a few core functions
    /// </summary>
    public abstract class SolarisInteractionModuleBase : InteractionModuleBase
    {
        /// <summary>
        /// Returns the user of the context as SGU
        /// </summary>
        protected SocketGuildUser? GetGuildUser()
        {
            if (Context.User is SocketGuildUser gUser)
                return gUser;
            return null;
        }

        /// <summary>
        /// Returns the interaction
        /// </summary>
        protected IDiscordInteraction Interaction => Context.Interaction;

        /// <summary>
        /// Gets an interaction tag for logging
        /// </summary>
        protected string GetIntTag() => $"[Int {Context.Interaction.Id}]";
    }
}
