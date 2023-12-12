using Discord;
using Discord.Interactions;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
namespace SolarisBot.Discord.Modules.Utility
{
    [Module("utility"), Group("utility", "Utility commands"), RequireContext(ContextType.Guild)]
    internal class UtilityCommands : SolarisInteractionModuleBase
    {
        //So far we need no constructor here as this has no dependencies

        [SlashCommand("get-pfp", "Get a users PFP"), UserCommand("Get PFP")]
        public async Task GetUserPfpAsync(IUser user) //todo: [TESTING] Does this format correctly?
        {
            var gUser = GetGuildUser(user);

            var strings = new List<string>();

            var defaultAvatar = gUser.GetAvatarUrl();
            if (defaultAvatar is not null)
                strings.Add($"Default:\n{defaultAvatar}");

            var guildAvatar = gUser.GetGuildAvatarUrl();
            if (guildAvatar is not null)
                strings.Add($"Guild:\n{guildAvatar}");

            if (strings.Count == 0)
            {
                await Interaction.ReplyErrorAsync(GenericError.NoResults);
                return;
            }

            var response = string.Join("\n\n", strings);
            await Interaction.ReplyAsync(response);
        }

        [SlashCommand("gen-embed", "Generate an embed from JSON")] //todo: [TESTING] Does this allow users to use everyone/here pings?
        public async Task GenerateEmbedAsync(string json)
        {
            if (!EmbedBuilder.TryParse(json, out var embed))
            {
                await Interaction.ReplyErrorAsync("Failed to generate embed from supplied JSON");
                return;
            }
            await Interaction.ReplyAsync(embed.Build());
        }
    }
}
