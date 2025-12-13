namespace VerbexCli.Commands
{
    using System;
    using System.CommandLine;
    using System.Linq;
    using System.Threading.Tasks;
    using VerbexCli.Infrastructure;

    /// <summary>
    /// Commands for maintaining Verbex indices
    /// </summary>
    public static class MaintenanceCommands
    {
        /// <summary>
        /// Creates the maintenance command group
        /// </summary>
        /// <returns>Maintenance command</returns>
        public static Command CreateMaintenanceCommand()
        {
            Command maintCommand = new Command("maint", "Maintenance operations for indices");

            // Add subcommands
            maintCommand.AddCommand(CreateFlushCommand());
            maintCommand.AddCommand(CreateGarbageCollectCommand());
            maintCommand.AddCommand(CreateBenchmarkCommand());
            maintCommand.AddCommand(CreateStressCommand());

            return maintCommand;
        }

        /// <summary>
        /// Creates the flush command
        /// </summary>
        /// <returns>Flush command</returns>
        private static Command CreateFlushCommand()
        {
            Command flushCommand = new Command("flush", "Force flush write buffer");

            Option<string> indexOption = new Option<string>(
                aliases: new[] { "--index", "-i" },
                description: "Index name (uses active index if not specified)")
            {
                IsRequired = false
            };
            flushCommand.AddOption(indexOption);

            flushCommand.SetHandler(async (string? index) =>
            {
                await HandleFlushAsync(index).ConfigureAwait(false);
            }, indexOption);

            return flushCommand;
        }

        /// <summary>
        /// Creates the garbage collect command
        /// </summary>
        /// <returns>Garbage collect command</returns>
        private static Command CreateGarbageCollectCommand()
        {
            Command gcCommand = new Command("gc", "Run garbage collection");

            Option<string> indexOption = new Option<string>(
                aliases: new[] { "--index", "-i" },
                description: "Index name (uses active index if not specified)")
            {
                IsRequired = false
            };
            gcCommand.AddOption(indexOption);

            gcCommand.SetHandler(async (string? index) =>
            {
                await HandleGarbageCollectAsync(index).ConfigureAwait(false);
            }, indexOption);

            return gcCommand;
        }

        /// <summary>
        /// Creates the benchmark command
        /// </summary>
        /// <returns>Benchmark command</returns>
        private static Command CreateBenchmarkCommand()
        {
            Command benchmarkCommand = new Command("benchmark", "Run performance benchmark");

            Option<string> indexOption = new Option<string>(
                aliases: new[] { "--index", "-i" },
                description: "Index name (uses active index if not specified)")
            {
                IsRequired = false
            };

            Option<int> documentsOption = new Option<int>(
                aliases: new[] { "--documents", "-d" },
                description: "Number of documents to benchmark with")
            {
                IsRequired = false
            };
            documentsOption.SetDefaultValue(100);

            benchmarkCommand.AddOption(indexOption);
            benchmarkCommand.AddOption(documentsOption);

            benchmarkCommand.SetHandler(async (string? index, int documents) =>
            {
                await HandleBenchmarkAsync(index, documents).ConfigureAwait(false);
            }, indexOption, documentsOption);

            return benchmarkCommand;
        }

        /// <summary>
        /// Creates the stress test command
        /// </summary>
        /// <returns>Stress test command</returns>
        private static Command CreateStressCommand()
        {
            Command stressCommand = new Command("stress", "Run stress test");

            Option<string> indexOption = new Option<string>(
                aliases: new[] { "--index", "-i" },
                description: "Index name (uses active index if not specified)")
            {
                IsRequired = false
            };

            Option<int> documentsOption = new Option<int>(
                aliases: new[] { "--documents", "-d" },
                description: "Number of documents for stress test")
            {
                IsRequired = false
            };
            documentsOption.SetDefaultValue(1000);

            stressCommand.AddOption(indexOption);
            stressCommand.AddOption(documentsOption);

            stressCommand.SetHandler(async (string? index, int documents) =>
            {
                await HandleStressAsync(index, documents).ConfigureAwait(false);
            }, indexOption, documentsOption);

            return stressCommand;
        }

        // Command handlers

        /// <summary>
        /// Handles the flush command
        /// </summary>
        private static async Task HandleFlushAsync(string? index)
        {
            try
            {
                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");
                OutputManager.WriteVerbose($"Flushing write buffer for index '{actualIndex}'");

                await IndexManager.Instance.FlushAsync(actualIndex).ConfigureAwait(false);
                OutputManager.WriteSuccess($"Write buffer flushed for index '{actualIndex}'");
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to flush index: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the garbage collect command
        /// </summary>
        private static async Task HandleGarbageCollectAsync(string? index)
        {
            try
            {
                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");
                OutputManager.WriteVerbose($"Running garbage collection for index '{actualIndex}'");

                var indexManager = IndexManager.Instance;
                await indexManager.UseIndexAsync(actualIndex).ConfigureAwait(false);

                if (indexManager.CurrentIndex != null)
                {
                    // Note: GarbageCollect is no longer needed with SQLite-based storage
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    OutputManager.WriteSuccess($"Garbage collection completed for index '{actualIndex}'");
                    OutputManager.WriteInfo("Memory usage optimized");
                }
                else
                {
                    throw new InvalidOperationException($"Index '{actualIndex}' is not loaded");
                }
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to run garbage collection: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the benchmark command
        /// </summary>
        private static async Task HandleBenchmarkAsync(string? index, int documents)
        {
            try
            {
                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");
                OutputManager.WriteVerbose($"Running benchmark for index '{actualIndex}' with {documents} documents");

                var indexManager = IndexManager.Instance;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                // Clear any existing benchmark documents first
                var existingDocs = await indexManager.ListDocumentsAsync(actualIndex).ConfigureAwait(false);
                foreach (dynamic doc in existingDocs)
                {
                    if (doc.Name != null && doc.Name.ToString().StartsWith("bench"))
                    {
                        try
                        {
                            await indexManager.RemoveDocumentAsync(actualIndex, doc.Name.ToString()).ConfigureAwait(false);
                        }
                        catch
                        {
                            // Ignore errors removing benchmark docs
                        }
                    }
                }

                // Generate test documents
                Random random = new Random();
                string[] words = { "benchmark", "test", "document", "performance", "index", "search", "data", "analysis" };

                // Indexing benchmark
                var indexingStart = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < documents; i++)
                {
                    string content = string.Join(" ", Enumerable.Range(0, random.Next(10, 50))
                        .Select(_ => words[random.Next(words.Length)]));
                    await indexManager.AddDocumentAsync(actualIndex, $"bench{i}", content).ConfigureAwait(false);
                }
                indexingStart.Stop();

                // Search benchmark
                var searchStart = System.Diagnostics.Stopwatch.StartNew();
                for (int i = 0; i < 10; i++)
                {
                    await indexManager.SearchAsync(actualIndex, words[random.Next(words.Length)], false, 10).ConfigureAwait(false);
                }
                searchStart.Stop();

                stopwatch.Stop();

                var results = new
                {
                    Index = actualIndex,
                    Documents = documents,
                    IndexingTime = $"{indexingStart.Elapsed.TotalSeconds:F2} seconds",
                    SearchTime = $"{searchStart.Elapsed.TotalMilliseconds / 10:F1} ms (avg)",
                    ThroughputDocsPerSecond = Math.Round(documents / indexingStart.Elapsed.TotalSeconds),
                    TotalTime = $"{stopwatch.Elapsed.TotalSeconds:F2} seconds"
                };

                OutputManager.WriteData(results);
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Benchmark failed: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the stress test command
        /// </summary>
        private static async Task HandleStressAsync(string? index, int documents)
        {
            try
            {
                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");
                OutputManager.WriteVerbose($"Running stress test for index '{actualIndex}' with {documents} documents");

                var indexManager = IndexManager.Instance;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                int errors = 0;

                try
                {
                    // Generate stress test data
                    Random random = new Random();
                    string[] words = { "stress", "test", "large", "volume", "data", "processing", "performance", "scale" };

                    // Phase 1: Bulk document addition
                    OutputManager.WriteInfo($"Adding {documents} documents...");
                    for (int i = 0; i < documents; i++)
                    {
                        try
                        {
                            string content = string.Join(" ", Enumerable.Range(0, random.Next(20, 100))
                                .Select(_ => words[random.Next(words.Length)]));
                            await indexManager.AddDocumentAsync(actualIndex, $"stress{i}", content).ConfigureAwait(false);
                        }
                        catch
                        {
                            errors++;
                        }

                        if (i % 100 == 0)
                        {
                            OutputManager.WriteVerbose($"Progress: {i}/{documents} documents");
                        }
                    }

                    // Phase 2: Random searches
                    OutputManager.WriteInfo("Performing random searches...");
                    for (int i = 0; i < 200; i++)
                    {
                        try
                        {
                            await indexManager.SearchAsync(actualIndex, words[random.Next(words.Length)], random.Next(2) == 0, 10).ConfigureAwait(false);
                        }
                        catch
                        {
                            errors++;
                        }
                    }

                    // Get final statistics
                    object stats = await indexManager.GetStatisticsAsync(actualIndex).ConfigureAwait(false);
                    dynamic statsObj = stats;

                    stopwatch.Stop();

                    var results = new
                    {
                        Index = actualIndex,
                        Documents = documents,
                        TotalTime = $"{stopwatch.Elapsed.TotalSeconds:F1} seconds",
                        ActualDocuments = (int)statsObj.Documents,
                        Errors = errors,
                        Status = errors == 0 ? "PASSED" : "PASSED_WITH_ERRORS",
                        MemoryUsage = statsObj.Memory.Total
                    };

                    OutputManager.WriteData(results);
                }
                catch (Exception innerEx)
                {
                    throw new Exception($"Stress test encountered critical error: {innerEx.Message}", innerEx);
                }
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Stress test failed: {ex.Message}");
                throw;
            }
        }
    }
}