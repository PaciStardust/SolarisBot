using Discord;
using Discord.Interactions;
using Newtonsoft.Json;
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
            var gUser = GetGuildUser(user);

            var strings = new List<string>();

            var defaultAvatar = gUser.GetAvatarUrl();
            if (defaultAvatar is not null)
                strings.Add($"Default: *{defaultAvatar}*");

            var guildAvatar = gUser.GetGuildAvatarUrl();
            if (guildAvatar is not null)
                strings.Add($"Guild: *{guildAvatar}*");

            if (strings.Count == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var response = string.Join("\n", strings);
            await Interaction.ReplyPlaintextAsync(response);
        }
    }
}
