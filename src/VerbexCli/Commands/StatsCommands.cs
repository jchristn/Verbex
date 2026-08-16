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

            Option<string> indexOption = new Option<string>("--index", "-i")
            {
                Description = "Index name (uses active index if not specified)"
            };

            Option<string> termOption = new Option<string>("--term", "-t")
            {
                Description = "Show statistics for a specific term"
            };

            Option<bool> cacheOption = new Option<bool>("--cache", "-c")
            {
                Description = "Show cache statistics"
            };

            statsCommand.Options.Add(indexOption);
            statsCommand.Options.Add(termOption);
            statsCommand.Options.Add(cacheOption);

            statsCommand.SetAction(async (ParseResult parseResult) =>
            {
                string? index = parseResult.GetValue(indexOption);
                string? term = parseResult.GetValue(termOption);
                bool cache = parseResult.GetValue(cacheOption);
                await HandleStatsAsync(index, term, cache).ConfigureAwait(false);
            });

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
                    IndexManager indexManager = IndexManager.Instance;
                    await indexManager.UseIndexAsync(actualIndex).ConfigureAwait(false);

                    if (indexManager.CurrentIndex != null)
                    {
                        Verbex.Models.TermStatisticsResult? termStats = await indexManager.CurrentIndex.GetTermStatisticsAsync(term).ConfigureAwait(false);
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
                    object cacheStats = new
                    {
                        CacheStatistics = statsObj.CacheStatistics,
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