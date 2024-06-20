using Discord;
using Discord.Interactions;
using Discord.WebSocket;

namespace SolarisBot.Discord.Common
{
    /// <summary>
    /// Extended InteractionModeuleBase with a few core functions
    /// </summary>
    public abstract class SolarisInteractionModuleBase : InteractionModuleBase //todo: [REFACTOR] Move most functionality into services with short lifetime
    { //todo: [REFACTOR] Document all methods?
        /// <summary>
        /// Converts user to SGU
        /// </summary>
        /// <param name="user">User to convert</param>
        /// <returns>Converted user</returns>
        /// <exception cref="ArgumentException">Thows an argumentexception if proviced user is not from guild</exception>
        protected static SocketGuildUser GetGuildUser(IUser user) //todo: [REFACTOR] REMOVAL
        {
            if (user is SocketGuildUser gUser)
                return gUser;
            throw new ArgumentException("Unable to convert user go guilduser");
        }

        /// <summary>
        /// Returns the interaction
        /// </summary>
        protected IDiscordInteraction Interaction => Context.Interaction; //todo: [REFACTOR] REMOVAL

        /// <summary>
        /// Gets an interaction tag for logging
        /// </summary>
        protected string GetIntTag() => $"[Int {Context.Interaction.Id}]"; //todo: [REFACTOR] REMOVAL
    }
}
