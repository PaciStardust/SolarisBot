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
        /// Converts user to SGU
        /// </summary>
        /// <param name="user">User to convert</param>
        /// <returns>Converted user</returns>
        /// <exception cref="ArgumentException">Thows an argumentexception if proviced user is not from guild</exception>
        protected static SocketGuildUser GetGuildUser(IUser user)
        {
            if (user is SocketGuildUser gUser)
                return gUser;
            throw new ArgumentException("Unable to convert user go guilduser");
        }

        /// <summary>
        /// Returns the interaction
        /// </summary>
        protected IDiscordInteraction Interaction => Context.Interaction;

        /// <summary>
        /// Gets an interaction tag for logging
        /// </summary>
        protected string GetIntTag() => $"[Int {Context.Interaction.Id}]";

        /// <summary>
        /// Gets a role by ID
        /// </summary>
        /// <param name="id">ID of role</param>
        /// <returns>A role matching the ID or null if none could be found</returns>
        protected IRole? FindRole(ulong id)
            => Context.Guild.Roles.FirstOrDefault(r => r.Id == id);
    }
}
