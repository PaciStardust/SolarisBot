using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
namespace SolarisBot.Discord.Modules.Utility
{
    [Module("utility"), Group("utility", "Utility commands"), RequireContext(ContextType.Guild)]
    internal class UtilityCommands : SolarisInteractionModuleBase
    {
        //So far we need no constructor here as this has no dependencies

        [SlashCommand("get-pfp", "Get a users PFP"), UserCommand("Get PFP")]
        public async Task GetUserPfpAsync(IUser user)
        {
            if (user is not SocketGuildUser gUser)
            {
                await Interaction.ReplyErrorAsync(StandardError.FailedConversion("User", "SocketGuildUser"));
                return;
            }

            var strings = new List<string>();

            var defaultAvatar = gUser.GetAvatarUrl();
            if (defaultAvatar is not null)
                strings.Add($"Default: *{defaultAvatar}*");

            var guildAvatar = gUser.GetGuildAvatarUrl();
            if (guildAvatar is not null)
                strings.Add($"Guild: *{guildAvatar}*");

            if (strings.Count == 0)
            {
                await Interaction.ReplyErrorAsync(StandardError.NoResults);
                return;
            }

            var response = string.Join("\n", strings);
            await Interaction.ReplyPlaintextAsync(response);
        }
    }
}
