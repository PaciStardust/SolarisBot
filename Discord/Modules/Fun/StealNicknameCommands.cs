using Discord.Interactions;
using Discord;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Common;

namespace SolarisBot.Discord.Modules.Fun
{
    [Module("fun/stealnickname")]
    internal class StealNicknameCommands : SolarisInteractionModuleBase
    {
        private readonly ILogger<StealNicknameCommands> _logger;
        private readonly DatabaseContext _dbContext;
        internal StealNicknameCommands(ILogger<StealNicknameCommands> logger, DatabaseContext dbctx)
        {
            _dbContext = dbctx;
            _logger = logger;
        }

        [SlashCommand("cfg-stealnick", "[MANAGE NICKS ONLY] Set up nickname stealing"), RequireBotPermission(GuildPermission.ManageNicknames), RequireUserPermission(GuildPermission.ManageNicknames)]
        public async Task ConfigureMagicAsync
        (
            [Summary(description: "Is feature enabled?")] bool enabled
        )
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.StealNicknameOn = enabled;

            _logger.LogDebug("{intTag} Setting nickname stealing to {enabled} in guild {guild}", GetIntTag(), guild.StealNicknameOn, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set nickname stealing to {enabled} in guild {guild}", GetIntTag(), guild.StealNicknameOn, Context.Guild.Log());
            await Interaction.ReplyAsync($"Nickname stealing is currently **{(guild.StealNicknameOn ? "enabled" : "disabled")}**");
        }

        [UserCommand("Steal Nickname"), SlashCommand("stealnick", "Steal a persons nick"), RequireBotPermission(GuildPermission.ManageNicknames)]
        public async Task StealNicknameUserAsync(IUser user) //todo: [TESTING] Does renaming still error?
        {
            if (Context.User.Id == user.Id)
            {
                await Interaction.ReplyErrorAsync("You can not steal from yourself");
                return;
            }
            if (user.IsBot || user.IsWebhook)
            {
                await Interaction.ReplyErrorAsync("You can not steal from bots");
                return;
            }
            if (user.Id == Context.Guild.OwnerId)
            {
                await Interaction.ReplyErrorAsync("You can not steal from owners");
                return;
            }
            if (Context.User.Id == Context.Guild.OwnerId)
            {
                await Interaction.ReplyErrorAsync("Owners can not steal");
                return;
            }

            var gUser = GetGuildUser(Context.User);
            var gUserName = gUser.DisplayName;
            if (gUserName.Length >= 32) //Max length for nicknames
            {
                await Interaction.ReplyErrorAsync("Your name is too long for stealing");
                return;
            }

            var gTargetUser = GetGuildUser(user);
            var gTargetName = gTargetUser.DisplayName;
            if (gTargetName.Length <= 1)
            {
                await Interaction.ReplyErrorAsync("Target name is too short for stealing");
                return;
            }

            var dbGuild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);

            if (!dbGuild?.StealNicknameOn ?? true)
            {
                await Interaction.ReplyErrorAsync("Nickname stealing is not enabled in this guild");
                return;
            }

            var stealIndex = Utils.Faker.Random.Int(0, gTargetName.Length - 1);
            var stolenLetter = gTargetName[stealIndex];
            var gTargetNameNew = gTargetName.Remove(stealIndex, 1);
            var insertIndex = Utils.Faker.Random.Int(0, gUserName.Length);
            var gNameNew = insertIndex == gUserName.Length ? gUserName + stolenLetter
                : gUserName.Insert(insertIndex, stolenLetter.ToString());

            _logger.LogDebug("{intTag} Renaming user {user} => {renamed} and {targetUser} => {targetRenamed} after stealing nick", GetIntTag(), gUser.Log(), gNameNew, gTargetUser.Log(), gTargetNameNew);
            await gUser.ModifyAsync(x => x.Nickname = gNameNew);
            await gTargetUser.ModifyAsync(x => x.Nickname = gTargetNameNew);
            _logger.LogInformation("{intTag} Renamed user {user} => {renamed} and {targetUser} => {targetRenamed} after stealing nick", GetIntTag(), gUser.Log(), gNameNew, gTargetUser.Log(), gTargetNameNew);

            await Interaction.ReplyAsync($"**{gUserName}***({gUser.Mention})* stole the letter **{stolenLetter}** from **{gTargetName}***({gTargetUser.Mention})*");
        }
    }
}
