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
        /// Returns the interaction
        /// </summary>
        protected IDiscordInteraction Interaction => Context.Interaction;
    }
}
