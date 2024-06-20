using SolarisBot.Discord.Common.Attributes;

namespace SolarisBot.Discord.Services
{
    [AutoLoadService]
    internal sealed class StatisticsService
    {
        internal DateTime TimeStarted { get; } = DateTime.Now;
        internal uint CommandsExecuted { get; private set; } = 0;
        internal uint CommandsFailed { get; private set; } = 0;

        /// <summary>
        /// Increases counter of failed commands
        /// </summary>
        internal void IncreaseCommandsFailed()
            => CommandsFailed++;

        /// <summary>
        /// Increases counter of executed commands
        /// </summary>
        internal void IncreaseCommandsExecuted()
            => CommandsExecuted++;
    }
}
