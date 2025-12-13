namespace VerbexCli.Commands
{
    using System;
    using System.CommandLine;
    using System.Threading.Tasks;
    using VerbexCli.Infrastructure;

    /// <summary>
    /// Commands for showing statistics about Verbex indices
    /// </summary>
    public static class StatsCommands
    {
        /// <summary>
        /// Creates the stats command
        /// </summary>
        /// <returns>Stats command</returns>
        public static Command CreateStatsCommand()
        {
            Command statsCommand = new Command("stats", "Show statistics for an index");

            Option<string> indexOption = new Option<string>(
                aliases: new[] { "--index", "-i" },
                description: "Index name (uses active index if not specified)")
            {
                IsRequired = false
            };

            Option<string> termOption = new Option<string>(
                aliases: new[] { "--term", "-t" },
                description: "Show statistics for a specific term")
            {
                IsRequired = false
            };

            Option<bool> cacheOption = new Option<bool>(
                aliases: new[] { "--cache", "-c" },
                description: "Show cache statistics")
            {
                IsRequired = false
            };

            statsCommand.AddOption(indexOption);
            statsCommand.AddOption(termOption);
            statsCommand.AddOption(cacheOption);

            statsCommand.SetHandler(async (string? index, string? term, bool cache) =>
            {
                await HandleStatsAsync(index, term, cache).ConfigureAwait(false);
            }, indexOption, termOption, cacheOption);

            return statsCommand;
        }

        /// <summary>
        /// Handles the stats command
        /// </summary>
        private static async Task HandleStatsAsync(string? index, string? term, bool cache)
        {
            try
            {
                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");
                if (!string.IsNullOrEmpty(term))
                {
                    OutputManager.WriteVerbose($"Showing term statistics for '{term}' in index '{actualIndex}'");

                    // Get the index to query term stats
                    var indexManager = IndexManager.Instance;
                    await indexManager.UseIndexAsync(actualIndex).ConfigureAwait(false);

                    if (indexManager.CurrentIndex != null)
                    {
                        Verbex.Repositories.TermStatisticsResult? termStats = await indexManager.CurrentIndex.GetTermStatisticsAsync(term).ConfigureAwait(false);
                        if (termStats != null)
                        {
                            object result = new
                            {
                                Term = term,
                                DocumentFrequency = termStats.DocumentFrequency,
                                TotalFrequency = termStats.TotalFrequency,
                                AverageFrequency = termStats.DocumentFrequency > 0
                                    ? Math.Round((double)termStats.TotalFrequency / termStats.DocumentFrequency, 2)
                                    : 0.0
                            };
                            OutputManager.WriteData(result);
                        }
                        else
                        {
                            OutputManager.WriteWarning($"Term '{term}' not found in index '{actualIndex}'");
                        }
                    }
                }
                else if (cache)
                {
                    OutputManager.WriteVerbose($"Showing cache statistics for index '{actualIndex}'");

                    object stats = await IndexManager.Instance.GetStatisticsAsync(actualIndex).ConfigureAwait(false);

                    // Extract cache-specific information
                    dynamic statsObj = stats;
                    var cacheStats = new
                    {
                        HotCacheSize = statsObj.Cache.HotCacheSize,
                        DocumentCacheSize = statsObj.Cache.DocumentCacheSize,
                        WriteBufferSize = statsObj.Cache.WriteBufferSize,
                        Memory = statsObj.Memory
                    };

                    OutputManager.WriteData(cacheStats);
                }
                else
                {
                    OutputManager.WriteVerbose($"Showing general statistics for index '{actualIndex}'");

                    object stats = await IndexManager.Instance.GetStatisticsAsync(actualIndex).ConfigureAwait(false);
                    OutputManager.WriteData(stats);
                }
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to get statistics: {ex.Message}");
                throw;
            }
        }
    }
}