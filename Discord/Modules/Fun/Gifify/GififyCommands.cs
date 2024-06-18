using Discord.Interactions;
using Discord;
using SolarisBot.Discord.Common;
using SolarisBot.Discord.Common.Attributes;
using SolarisBot.Discord.Modules.Fun.Gifify;

namespace SolarisBot.Discord.Modules.Fun
{
    [Module("fun/gifify")]
    internal class GififyCommands : SolarisInteractionModuleBase
    {
        private readonly GififyService _gififyService;
        internal GififyCommands(GififyService gififyService)
        {
            _gififyService = gififyService;
        }

        [SlashCommand("cfg-gifify", "[MANAGE MSGS ONLY] Set up gif conversion"), RequireUserPermission(GuildPermission.ManageMessages)]
        public async Task ConfigureGifify
        (
            [Summary(description: "Is feature enabled?")] bool enabled
        )
        {
            var res = await _gififyService.ConfigureAsync(Context.Guild, enabled);
            await res.Match(
                success => Interaction.ReplyAsync($"Gif conversion is currently **{(success.Value.GififyOn ? "enabled" : "disabled")}**"),
                err => Interaction.ReplyErrorAsync(err.Value)
            );
        }

        [MessageCommand("Gifiy")]
        public async Task GififyMessageAsync(IMessage message)
        {
            await Interaction.DeferAsync(true);

            foreach (var attachment in message.Attachments)
            {
                var res = await _gififyService.GififyAsync(Context.Guild, attachment);
                await res.Match(
                    async success =>
                    {
                        await Interaction.ReplyAttachmentAsync(success.Value, false);
                        success.Value.Dispose();
                    },
                    err => Interaction.ReplyErrorAsync(err.Value)
                );
            }
        }

        [SlashCommand("gifify", "Convert image to gif")]
        public async Task GififySlashAsync
        (
            [Summary(description: "Image to convert to gif")] IAttachment image,
            [Summary(description: "[Opt] Only visible locally?")] bool isPrivate = false
        )
        {
            await Interaction.DeferAsync(isPrivate);

            var res = await _gififyService.GififyAsync(Context.Guild, image);
            await res.Match(
                async success =>
                {
                    await Interaction.ReplyAttachmentAsync(success.Value, isPrivate);
                    success.Value.Dispose();
                },
                err => Interaction.ReplyErrorAsync(err.Value)
            );
        }
    }
}
