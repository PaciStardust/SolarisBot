using SolarisBot.Database;
using SolarisBot.Discord;

namespace SolarisBot
{
    internal static class Program
    {
        public static async Task Main(string[] args)
        {
            if (Config.Discord.StatupBot)
                await DiscordClient.Start();

            await Task.Delay(-1);
        }
    }
}