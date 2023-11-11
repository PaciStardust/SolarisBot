using Discord.Interactions;
using Discord;
using Microsoft.Extensions.Logging;
using SolarisBot.Database;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Common;
using Bogus;
using Discord.Net;

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

        [SlashCommand("cfg-stealnick", "[MANAGE ROLES ONLY] Set up nickname stealing"), RequireBotPermission(GuildPermission.ManageNicknames), RequireUserPermission(GuildPermission.ManageNicknames)]
        public async Task ConfigureMagicAsync(bool stealing = false)
        {
            var guild = await _dbContext.GetOrCreateTrackedGuildAsync(Context.Guild.Id);

            guild.StealNicknameOn = stealing;

            _logger.LogDebug("{intTag} Setting nickname stealing to {enabled} in guild {guild}", GetIntTag(), guild.StealNicknameOn, Context.Guild.Log());
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("{intTag} Set nickname stealing to {enabled} in guild {guild}", GetIntTag(), guild.StealNicknameOn, Context.Guild.Log());
            await Interaction.ReplyAsync($"Nickname stealing is currently **{(guild.StealNicknameOn ? "enabled" : "disabled")}**");
        }

        [UserCommand("Steal Nickname"), SlashCommand("stealnick", "Steal a persons nick"), RequireBotPermission(GuildPermission.ManageNicknames)]
        public async Task StealNicknameUserAsync(IUser user)
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
            var gTargetUser = GetGuildUser(user);

            var dbGuild = await _dbContext.GetGuildByIdAsync(Context.Guild.Id);

            if (!dbGuild?.StealNicknameOn ?? true)
            {
                await Interaction.ReplyErrorAsync("Magic is not enabled in this guild");
                return;
            }

            var stealIndex = Utils.Faker.Random.Int(0, gTargetUser.DisplayName.Length - 1);
            var stolenLetter = gTargetUser.DisplayName[stealIndex];
            var gTargetNameNew = gTargetUser.DisplayName.Remove(stealIndex, 1);
            var insertIndex = Utils.Faker.Random.Int(0, gTargetUser.DisplayName.Length);
            var gNameNew = gUser.DisplayName.Insert(insertIndex, stolenLetter.ToString());

            await gUser.ModifyAsync(x => x.Nickname = gNameNew);
            await gTargetUser.ModifyAsync(x => x.Nickname = gTargetNameNew);

            await Interaction.ReplyAsync($"{gUser.Mention} stole the letter \"{stolenLetter}\" from {gTargetUser.Mention}");
        }
    }
}
