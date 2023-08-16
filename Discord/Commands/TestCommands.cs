using Discord.Interactions;
using Discord;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SolarisBot.Discord.Commands
{
    [RequireContext(ContextType.Guild)]
    public sealed class TestCommands : SolarisInteractionModuleBase
    {
        [MessageCommand("Repeat")]
        public async Task RepeatAsync(IMessage msg)
        {
            await RespondEmbedAsync("Repeat Message", msg.CleanContent);
        }
    }
}
