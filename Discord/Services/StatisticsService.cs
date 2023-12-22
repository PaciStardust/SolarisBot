using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Services
{
    [AutoLoadService]
    internal sealed class StatisticsService
    {
        internal DateTime TimeStarted { get; } = DateTime.Now;
        internal uint CommandsExecuted { get; private set; } = 0;
        internal uint CommandsFailed { get; private set; } = 0;

        internal void IncreaseCommandsFailed()
            => CommandsFailed++;

        internal void IncreaseCommandsExecuted()
            => CommandsExecuted++;
    }
}
