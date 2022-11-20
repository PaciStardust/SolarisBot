using Discord;
using Discord.Interactions;
using SolarisBot.Database;

namespace SolarisBot.Discord.Commands
{
    [RequireOwner, Group("owner", "[OWNER ONLY] Bot Owner Commands")]
    public class CmdOwner : InteractionModuleBase
    {
        [SlashCommand("reload-cmd", "[OWNER ONLY] Reload all commands globally, takes up to 1h")]
        public async Task ReloadCommands()
        {
            await DiscordClient.UpdateCommands();
            await RespondAsync(embed: Embeds.Info("Reloading Commands", "Globally reloading all commands, this may take up to an hour", Embeds.ColorImportant));
        }

        [SlashCommand("reload-cmd-guild", "[OWNER ONLY] Reload all commands for a guild")]
        public async Task ReloadCommands(string guildId)
        {
            if (!ulong.TryParse(guildId, out var guildIdConvert))
            {
                await RespondAsync(embed: Embeds.InvalidInput);
                return;
            }

            await DiscordClient.UpdateCommandsGuild(guildIdConvert);
            await RespondAsync(embed: Embeds.Info("Reloading Commands", "Reloading commands in guild " + guildIdConvert, Embeds.ColorImportant));
        }

        [SlashCommand("sql-run", "[OWNER ONLY] Run an SQL-RUN query")]
        public async Task SqlRun(string query)
        {
            Logger.Info($"A RUN query has been called manually \"{query}\"");
            var result = DbMain.Run(query, false);

            var embedText = $"{query}\n\n" + (result == -1 ? "Database encountered an error, please check logs for more details"
                : $"{result} changes have been made");

            await RespondAsync(embed: Embeds.Info("Executed SQL-RUN", $"```{embedText}```", Embeds.ColorImportant));
        }

        [SlashCommand("sql-get", "[OWNER ONLY] Run an SQL-GET query")]
        public async Task SqlGet(string query)
        {
            Logger.Warning($"A GET query has been called manually \"{query}\"");
            var result = DbMain.Get(query, false);

            if (result == null)
            {
                await RespondAsync(embed: Embeds.Info("Executed SQL-GET", "Database encountered an error, please check logs for more details", Embeds.ColorImportant));
                return;
            }

            var strings = new List<string>();
            var resCount = 1;
            while (result.Read())
            {
                var values = new List<string>();
                for (int i = 0; i < result.FieldCount; i++)
                {
                    var name = result.GetName(i);
                    var value = result.IsDBNull(i) ? "*NULL*" : result.GetValue(i).ToString() ?? "*UNKNOWN*";
                    values.Add($"{name} = {value}");
                }
                strings.Add($"Result {resCount}:\n{string.Join("\n", values)}");
                resCount++;
            }

            var embedText = $"{query}\n\n" + (strings.Count == 0 ? "No results found"
                : string.Join("\n\n", strings));

            if (embedText.Length > 1900)
                embedText = embedText[..1900] + "...";

            await RespondAsync(embed: Embeds.Info("Executed SQL-GET", $"```{embedText}```", Embeds.ColorImportant));
        }
        
        [SlashCommand("sql-metrics", "[OWNER-ONLY] Gets amount of run SQL queries")]
        public async Task SqlMetrics()
        {
            int[] values = { DbMain.ExecutedRun, DbMain.FailedRun, DbMain.ExecutedGet, DbMain.FailedGet };

            var runSum = values[0] + values[1];
            var getSum = values[2] + values[3];

            string[] metrics = 
            {
                "RUN Queries:",
                "> Exec: " + values[0],
                "> Fail: " + values[1],
                "> Sum : " + runSum,
                "",
                "GET Queries:",
                "> Exec: " + values[2],
                "> Fail: " + values[3],
                "> Sum : " + getSum,
                "",
                "RUN + GET sum:",
                "> Exec: " + (values[0] + values[2]),
                "> Fail: " + (values[1] + values[3]),
                "> Sum : " + (runSum + getSum)
            };

            await RespondAsync(embed: Embeds.Info("SQL-Metrics", $"```{string.Join("\n", metrics)}```"));
        }
    }
}
