namespace TestConsole
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex;
    using Verbex.Repositories;

    /// <summary>
    /// Processes all user commands and delegates to appropriate handlers.
    /// </summary>
    public class CommandProcessor
    {
        private readonly IndexManager _IndexManager;

        /// <summary>
        /// Initializes a new instance of the CommandProcessor class.
        /// </summary>
        /// <param name="indexManager">The index manager to use.</param>
        /// <exception cref="ArgumentNullException">Thrown when indexManager is null.</exception>
        public CommandProcessor(IndexManager indexManager)
        {
            _IndexManager = indexManager ?? throw new ArgumentNullException(nameof(indexManager));
        }

        /// <summary>
        /// Processes a user command asynchronously.
        /// </summary>
        /// <param name="command">The command to process.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public async Task ProcessCommandAsync(string command, CancellationToken cancellationToken = default)
        {
            string[] parts = ParseCommand(command);
            if (parts.Length == 0) return;

            string cmd = parts[0].ToLowerInvariant();

            switch (cmd)
            {
                case "help":
                case "?":
                    ShowHelpMessage();
                    break;

                case "index":
                    await HandleIndexCommandAsync(parts).ConfigureAwait(false);
                    break;

                case "add":
                    await HandleAddCommandAsync(parts).ConfigureAwait(false);
                    break;

                case "remove":
                    await HandleRemoveCommandAsync(parts).ConfigureAwait(false);
                    break;

                case "list":
                    await HandleListDocumentsCommandAsync().ConfigureAwait(false);
                    break;

                case "clear":
                    await HandleClearCommandAsync(parts).ConfigureAwait(false);
                    break;

                case "search":
                    await HandleSearchCommandAsync(parts).ConfigureAwait(false);
                    break;

                case "stats":
                    await HandleStatsCommandAsync(parts).ConfigureAwait(false);
                    break;

                case "debug":
                    HandleDebugCommand(parts);
                    break;

                case "flush":
                    await HandleFlushCommandAsync().ConfigureAwait(false);
                    break;

                case "demo":
                    await HandleDemoCommandAsync().ConfigureAwait(false);
                    break;

                case "benchmark":
                    await HandleBenchmarkCommandAsync().ConfigureAwait(false);
                    break;

                case "stress":
                    await HandleStressCommandAsync().ConfigureAwait(false);
                    break;

                case "export":
                    await HandleExportCommandAsync(parts).ConfigureAwait(false);
                    break;

                case "cls":
                    Console.Clear();
                    break;

                default:
                    Console.WriteLine($"Unknown command: {cmd}. Type 'help' for available commands.");
                    break;
            }
        }

        /// <summary>
        /// Shows detailed help message.
        /// </summary>
        private static void ShowHelpMessage()
        {
            Console.WriteLine("=== Verbex Interactive Test Console ===");
            Console.WriteLine();
            Console.WriteLine("INDEX MANAGEMENT:");
            Console.WriteLine("  index create [options]          Create a new index");
            Console.WriteLine("    --mode <memory|disk>          Storage mode (default: memory)");
            Console.WriteLine("    --name <name>                 Index name (default: default)");
            Console.WriteLine("    --lemmatizer                  Enable lemmatization");
            Console.WriteLine("    --stopwords                   Enable stop word removal");
            Console.WriteLine("    --min-length <n>              Minimum token length");
            Console.WriteLine("    --max-length <n>              Maximum token length");
            Console.WriteLine("  index use <name>                Switch to an existing index");
            Console.WriteLine("  index list                      List all available indices");
            Console.WriteLine("  index show                      Show current index configuration");
            Console.WriteLine("  index save <name>               Save current index to disk");
            Console.WriteLine("  index reload <name>             Reload index from disk");
            Console.WriteLine("  index discover                  Scan for existing persistent indices");
            Console.WriteLine();
            Console.WriteLine("DOCUMENT OPERATIONS:");
            Console.WriteLine("  add <name> [options]            Add a document");
            Console.WriteLine("    --content \"<text>\"            Document content (required if no --file)");
            Console.WriteLine("    --file <path>                 Load content from file (required if no --content)");
            Console.WriteLine("  remove <name>                   Remove document by name");
            Console.WriteLine("  list                            List all documents");
            Console.WriteLine("  clear [--force]                 Remove all documents (requires --force)");
            Console.WriteLine();
            Console.WriteLine("SEARCH:");
            Console.WriteLine("  search \"<query>\" [options]      Search documents");
            Console.WriteLine("    --and                         Use AND logic (default: OR)");
            Console.WriteLine("    --limit <n>                   Maximum results (default: 10)");
            Console.WriteLine();
            Console.WriteLine("ANALYSIS:");
            Console.WriteLine("  stats [<term>]                  Show index or term statistics");
            Console.WriteLine("  debug <term> [options]          Debug term processing");
            Console.WriteLine("    --lemmatizer                  Show lemmatization result");
            Console.WriteLine("    --stopwords                   Check if term is a stop word");
            Console.WriteLine();
            Console.WriteLine("MAINTENANCE:");
            Console.WriteLine("  flush                           Force flush pending writes (on-disk only)");
            Console.WriteLine();
            Console.WriteLine("TESTING:");
            Console.WriteLine("  demo                            Load sample documents");
            Console.WriteLine("  benchmark                       Run performance benchmark");
            Console.WriteLine("  stress                          Run stress test (1000 docs)");
            Console.WriteLine("  export <file>                   Export index data to JSON");
            Console.WriteLine();
            Console.WriteLine("OTHER:");
            Console.WriteLine("  cls                             Clear screen");
            Console.WriteLine("  exit/quit/q                     Exit application");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  index create --mode disk --name myindex --lemmatizer");
            Console.WriteLine("  index use myindex");
            Console.WriteLine("  add doc1 --content \"Hello world\"");
            Console.WriteLine("  add doc2 --file ./document.txt");
            Console.WriteLine("  search \"hello\"");
            Console.WriteLine("  search \"machine learning\" --and --limit 5");
            Console.WriteLine();
        }

        /// <summary>
        /// Parses a command string into parts, handling quoted strings.
        /// </summary>
        /// <param name="command">The command to parse.</param>
        /// <returns>Array of command parts.</returns>
        private static string[] ParseCommand(string command)
        {
            List<string> parts = new List<string>();
            bool inQuotes = false;
            string currentPart = "";

            for (int i = 0; i < command.Length; i++)
            {
                char c = command[i];

                if (c == '"' && (i == 0 || command[i - 1] != '\\'))
                {
                    inQuotes = !inQuotes;
                }
                else if (char.IsWhiteSpace(c) && !inQuotes)
                {
                    if (!string.IsNullOrEmpty(currentPart))
                    {
                        parts.Add(currentPart);
                        currentPart = "";
                    }
                }
                else
                {
                    currentPart += c;
                }
            }

            if (!string.IsNullOrEmpty(currentPart))
            {
                parts.Add(currentPart);
            }

            return parts.ToArray();
        }

        #region Index Commands

        private async Task HandleIndexCommandAsync(string[] parts)
        {
            if (parts.Length < 2)
            {
                Console.WriteLine("Usage: index <subcommand> [options]");
                Console.WriteLine("Subcommands: create, use, list, show, save, reload, discover");
                Console.WriteLine("Type 'help' for detailed usage.");
                return;
            }

            string subcommand = parts[1].ToLowerInvariant();

            switch (subcommand)
            {
                case "create":
                    await HandleIndexCreateAsync(parts).ConfigureAwait(false);
                    break;

                case "use":
                    await HandleIndexUseAsync(parts).ConfigureAwait(false);
                    break;

                case "list":
                    HandleIndexList();
                    break;

                case "show":
                    await HandleIndexShowAsync().ConfigureAwait(false);
                    break;

                case "save":
                    await HandleIndexSaveAsync(parts).ConfigureAwait(false);
                    break;

                case "reload":
                    await HandleIndexReloadAsync(parts).ConfigureAwait(false);
                    break;

                case "discover":
                    await HandleIndexDiscoverAsync().ConfigureAwait(false);
                    break;

                // Legacy support for direct mode specification
                case "memory":
                case "disk":
                    await HandleIndexCreateAsync(new[] { "index", "create", "--mode", subcommand }).ConfigureAwait(false);
                    break;

                default:
                    Console.WriteLine($"Unknown index subcommand: {subcommand}");
                    Console.WriteLine("Valid subcommands: create, use, list, show, save, reload, discover");
                    break;
            }
        }

        private async Task HandleIndexCreateAsync(string[] parts)
        {
            // Parse options
            StorageMode storageMode = StorageMode.InMemory;
            string configName = "default";
            bool enableLemmatizer = false;
            bool enableStopwords = false;
            int minLength = 0;
            int maxLength = 0;

            for (int i = 2; i < parts.Length; i++)
            {
                string opt = parts[i].ToLowerInvariant();

                switch (opt)
                {
                    case "--mode":
                        if (i + 1 < parts.Length)
                        {
                            string mode = parts[++i].ToLowerInvariant();
                            storageMode = mode switch
                            {
                                "memory" => StorageMode.InMemory,
                                "disk" => StorageMode.OnDisk,
                                _ => StorageMode.InMemory
                            };
                        }
                        break;

                    case "--name":
                        if (i + 1 < parts.Length)
                        {
                            configName = parts[++i];
                        }
                        break;

                    case "--lemmatizer":
                        enableLemmatizer = true;
                        break;

                    case "--stopwords":
                        enableStopwords = true;
                        break;

                    case "--min-length":
                        if (i + 1 < parts.Length && int.TryParse(parts[++i], out int min))
                        {
                            minLength = min;
                        }
                        break;

                    case "--max-length":
                        if (i + 1 < parts.Length && int.TryParse(parts[++i], out int max))
                        {
                            maxLength = max;
                        }
                        break;
                }
            }

            if (_IndexManager.CreateConfiguration(configName, storageMode, enableLemmatizer, enableStopwords, minLength, maxLength))
            {
                await _IndexManager.SwitchToIndexAsync(configName).ConfigureAwait(false);

                List<string> features = new List<string> { storageMode.ToString() };
                if (enableLemmatizer) features.Add("lemmatizer");
                if (enableStopwords) features.Add("stopwords");
                if (minLength > 0) features.Add($"min-length:{minLength}");
                if (maxLength > 0) features.Add($"max-length:{maxLength}");

                Console.WriteLine($"Created and switched to index '{configName}': {string.Join(", ", features)}");
            }
            else
            {
                Console.WriteLine($"Failed to create index '{configName}'.");
            }
        }

        private async Task HandleIndexUseAsync(string[] parts)
        {
            if (parts.Length < 3)
            {
                Console.WriteLine("Usage: index use <name>");
                Console.WriteLine("Use 'index list' to see available indices.");
                return;
            }

            string targetConfig = parts[2];
            await _IndexManager.SwitchToIndexAsync(targetConfig).ConfigureAwait(false);
        }

        private void HandleIndexList()
        {
            Console.WriteLine("=== Available Index Configurations ===");
            Console.WriteLine();

            foreach (KeyValuePair<string, IndexConfiguration> kvp in _IndexManager.AvailableConfigurations)
            {
                IndexConfiguration config = kvp.Value;
                string current = kvp.Key == _IndexManager.CurrentIndexName ? " (CURRENT)" : "";

                Console.WriteLine($"{config.Name}{current}");
                Console.WriteLine($"  Mode: {config.VerbexConfig.StorageMode}");
                Console.WriteLine($"  Lemmatizer: {(config.VerbexConfig.Lemmatizer != null ? "Yes" : "No")}");
                Console.WriteLine($"  Stop Words: {(config.VerbexConfig.StopWordRemover != null ? "Yes" : "No")}");
                Console.WriteLine($"  Token Length: {IndexManager.GetTokenLengthDescription(config.VerbexConfig)}");
                Console.WriteLine();
            }
        }

        private async Task HandleIndexShowAsync()
        {
            if (!_IndexManager.AvailableConfigurations.ContainsKey(_IndexManager.CurrentIndexName))
            {
                Console.WriteLine("No current configuration available.");
                return;
            }

            IndexConfiguration config = _IndexManager.AvailableConfigurations[_IndexManager.CurrentIndexName];
            Console.WriteLine($"=== Current Index: {config.Name} ===");
            Console.WriteLine();
            Console.WriteLine($"Storage Mode: {config.VerbexConfig.StorageMode}");
            Console.WriteLine($"Storage Directory: {config.VerbexConfig.StorageDirectory ?? "N/A"}");
            Console.WriteLine($"Lemmatizer: {(config.VerbexConfig.Lemmatizer != null ? "Enabled" : "Disabled")}");
            Console.WriteLine($"Stop Word Remover: {(config.VerbexConfig.StopWordRemover != null ? "Enabled" : "Disabled")}");
            Console.WriteLine($"Min Token Length: {(config.VerbexConfig.MinTokenLength > 0 ? config.VerbexConfig.MinTokenLength.ToString() : "None")}");
            Console.WriteLine($"Max Token Length: {(config.VerbexConfig.MaxTokenLength > 0 ? config.VerbexConfig.MaxTokenLength.ToString() : "None")}");

            if (_IndexManager.CurrentIndex != null)
            {
                Console.WriteLine();
                long docCount = await _IndexManager.CurrentIndex.GetDocumentCountAsync().ConfigureAwait(false);
                Console.WriteLine($"Documents: {docCount}");
            }
        }

        private async Task HandleIndexSaveAsync(string[] parts)
        {
            if (parts.Length < 3)
            {
                Console.WriteLine("Usage: index save <name>");
                return;
            }

            string indexName = parts[2];
            await _IndexManager.SaveCurrentIndexAsync(indexName).ConfigureAwait(false);
        }

        private async Task HandleIndexReloadAsync(string[] parts)
        {
            if (parts.Length < 3)
            {
                Console.WriteLine("Usage: index reload <name>");
                Console.WriteLine("Use 'index discover' to find available persistent indices.");
                return;
            }

            string indexName = parts[2];
            await _IndexManager.ReloadIndexAsync(indexName).ConfigureAwait(false);
        }

        private async Task HandleIndexDiscoverAsync()
        {
            Console.WriteLine("Scanning for persistent indices...");
            await _IndexManager.DiscoverExistingIndicesAsync().ConfigureAwait(false);

            List<KeyValuePair<string, IndexConfiguration>> persistentConfigs = _IndexManager.AvailableConfigurations
                .Where(kvp => kvp.Value.IsPersistent).ToList();

            if (persistentConfigs.Count == 0)
            {
                Console.WriteLine("No persistent indices found.");
            }
            else
            {
                Console.WriteLine($"Found {persistentConfigs.Count} persistent index(es):");
                foreach (KeyValuePair<string, IndexConfiguration> kvp in persistentConfigs)
                {
                    Console.WriteLine($"  {kvp.Value.Name} - {kvp.Value.VerbexConfig.StorageDirectory}");
                }
                Console.WriteLine("\nUse 'index reload <name>' to load a persistent index.");
            }
        }

        #endregion

        #region Document Commands

        private async Task HandleAddCommandAsync(string[] parts)
        {
            if (_IndexManager.CurrentIndex == null)
            {
                Console.WriteLine("No index initialized. Use 'index create' first.");
                return;
            }

            if (parts.Length < 2)
            {
                Console.WriteLine("Usage: add <name> --content \"<text>\" | --file <path>");
                return;
            }

            string name = parts[1];
            string? content = null;
            string? filePath = null;

            // Parse options
            for (int i = 2; i < parts.Length; i++)
            {
                string opt = parts[i].ToLowerInvariant();

                switch (opt)
                {
                    case "--content":
                        if (i + 1 < parts.Length)
                        {
                            content = parts[++i];
                        }
                        break;

                    case "--file":
                        if (i + 1 < parts.Length)
                        {
                            filePath = parts[++i];
                        }
                        break;
                }
            }

            // Validate: must have either content or file, not both
            if (content == null && filePath == null)
            {
                Console.WriteLine("Error: Must specify --content or --file");
                return;
            }

            if (content != null && filePath != null)
            {
                Console.WriteLine("Error: Cannot specify both --content and --file");
                return;
            }

            // Load content from file if specified
            if (filePath != null)
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"File not found: {filePath}");
                    return;
                }

                try
                {
                    content = await File.ReadAllTextAsync(filePath, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading file: {ex.Message}");
                    return;
                }
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                Console.WriteLine("Content cannot be empty.");
                return;
            }

            try
            {
                string fileName = filePath ?? $"{name}.txt";
                string docId = await _IndexManager.CurrentIndex.AddDocumentAsync(fileName, content).ConfigureAwait(false);
                _IndexManager.CurrentDocumentNames[name] = docId;

                Console.WriteLine($"Added document '{name}'");
                Console.WriteLine($"  ID: {docId}");
                Console.WriteLine($"  Length: {content.Length} characters");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error adding document: {ex.Message}");
            }
        }

        private async Task HandleRemoveCommandAsync(string[] parts)
        {
            if (_IndexManager.CurrentIndex == null)
            {
                Console.WriteLine("No index initialized. Use 'index create' first.");
                return;
            }

            if (parts.Length < 2)
            {
                Console.WriteLine("Usage: remove <name>");
                return;
            }

            string name = parts[1];

            if (!_IndexManager.CurrentDocumentNames.TryGetValue(name, out string? docId) || docId == null)
            {
                Console.WriteLine($"Document '{name}' not found.");
                return;
            }

            try
            {
                bool removed = await _IndexManager.CurrentIndex.RemoveDocumentAsync(docId, CancellationToken.None).ConfigureAwait(false);
                if (removed)
                {
                    _IndexManager.CurrentDocumentNames.Remove(name);
                    Console.WriteLine($"Removed document '{name}'.");
                }
                else
                {
                    Console.WriteLine($"Document '{name}' was not found in the index.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error removing document: {ex.Message}");
            }
        }

        private async Task HandleListDocumentsCommandAsync()
        {
            if (_IndexManager.CurrentIndex == null)
            {
                Console.WriteLine("No index initialized. Use 'index create' first.");
                return;
            }

            long docCount = await _IndexManager.CurrentIndex.GetDocumentCountAsync().ConfigureAwait(false);
            Console.WriteLine($"=== Documents ({docCount}) ===");

            if (_IndexManager.CurrentDocumentNames.Count == 0)
            {
                Console.WriteLine("No documents in the index.");
                return;
            }

            foreach (KeyValuePair<string, string> kvp in _IndexManager.CurrentDocumentNames)
            {
                try
                {
                    DocumentMetadata? metadata = await _IndexManager.CurrentIndex.GetDocumentAsync(kvp.Value, CancellationToken.None).ConfigureAwait(false);
                    if (metadata != null)
                    {
                        Console.WriteLine($"  {kvp.Key} - {metadata.DocumentLength} chars, {metadata.Terms.Count} terms");
                    }
                }
                catch
                {
                    Console.WriteLine($"  {kvp.Key} - (error retrieving metadata)");
                }
            }
        }

        private async Task HandleClearCommandAsync(string[] parts)
        {
            if (_IndexManager.CurrentIndex == null)
            {
                Console.WriteLine("No index initialized. Use 'index create' first.");
                return;
            }

            bool force = parts.Any(p => p.ToLowerInvariant() == "--force");

            if (!force)
            {
                Console.WriteLine("This will remove all documents. Use 'clear --force' to confirm.");
                return;
            }

            if (_IndexManager.CurrentDocumentNames.Count == 0)
            {
                Console.WriteLine("No documents to clear.");
                return;
            }

            try
            {
                int count = _IndexManager.CurrentDocumentNames.Count;
                List<string> docIds = _IndexManager.CurrentDocumentNames.Values.ToList();
                _IndexManager.CurrentDocumentNames.Clear();

                foreach (string docId in docIds)
                {
                    await _IndexManager.CurrentIndex.RemoveDocumentAsync(docId, CancellationToken.None).ConfigureAwait(false);
                }

                // Flush only for on-disk indices
                if (_IndexManager.CurrentIndex.Configuration?.StorageMode == StorageMode.OnDisk)
                {
                    await _IndexManager.CurrentIndex.FlushAsync(null, CancellationToken.None).ConfigureAwait(false);
                }
                Console.WriteLine($"Cleared {count} documents.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing documents: {ex.Message}");
            }
        }

        #endregion

        #region Search Commands

        private async Task HandleSearchCommandAsync(string[] parts)
        {
            if (_IndexManager.CurrentIndex == null)
            {
                Console.WriteLine("No index initialized. Use 'index create' first.");
                return;
            }

            if (parts.Length < 2)
            {
                Console.WriteLine("Usage: search \"<query>\" [--and] [--limit n]");
                return;
            }

            // Parse query and options
            string query = parts[1];
            bool useAndLogic = false;
            int? limit = null;

            for (int i = 2; i < parts.Length; i++)
            {
                string opt = parts[i].ToLowerInvariant();

                switch (opt)
                {
                    case "--and":
                        useAndLogic = true;
                        break;

                    case "--limit":
                        if (i + 1 < parts.Length && int.TryParse(parts[++i], out int limitValue))
                        {
                            limit = limitValue;
                        }
                        break;
                }
            }

            if (string.IsNullOrWhiteSpace(query))
            {
                Console.WriteLine("Query cannot be empty.");
                return;
            }

            try
            {
                SearchResults searchResults = await _IndexManager.CurrentIndex.SearchAsync(query, limit, useAndLogic, CancellationToken.None).ConfigureAwait(false);

                // Display results
                Console.WriteLine($"=== Search: \"{query}\" ({(useAndLogic ? "AND" : "OR")}) ===");
                Console.WriteLine($"Found: {searchResults.TotalCount} result(s) in {searchResults.SearchTime.TotalMilliseconds:F2}ms");
                Console.WriteLine();

                if (searchResults.TotalCount == 0)
                {
                    Console.WriteLine("No matching documents.");
                    return;
                }

                for (int i = 0; i < searchResults.Results.Count; i++)
                {
                    SearchResult result = searchResults.Results[i];
                    string? docName = GetDocumentName(result.DocumentId);

                    Console.WriteLine($"{i + 1}. {docName ?? result.Document?.DocumentPath ?? "Unknown"} (score: {result.Score:F4})");
                    Console.WriteLine($"   Matched terms: {result.MatchedTermCount}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error searching: {ex.Message}");
            }
        }

        #endregion

        #region Analysis Commands

        private async Task HandleStatsCommandAsync(string[] parts)
        {
            if (_IndexManager.CurrentIndex == null)
            {
                Console.WriteLine("No index initialized. Use 'index create' first.");
                return;
            }

            try
            {
                if (parts.Length >= 2)
                {
                    // Term-specific statistics
                    string term = parts[1];
                    TermStatisticsResult? termStats = await _IndexManager.CurrentIndex.GetTermStatisticsAsync(term, CancellationToken.None).ConfigureAwait(false);

                    if (termStats != null)
                    {
                        Console.WriteLine($"=== Term: {term} ===");
                        Console.WriteLine($"Document Frequency: {termStats.DocumentFrequency}");
                        Console.WriteLine($"Total Frequency: {termStats.TotalFrequency}");
                    }
                    else
                    {
                        Console.WriteLine($"Term '{term}' not found in the index.");
                    }
                }
                else
                {
                    // General index statistics
                    IndexStatistics stats = await _IndexManager.CurrentIndex.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

                    Console.WriteLine("=== Index Statistics ===");
                    Console.WriteLine($"Documents: {stats.DocumentCount:N0}");
                    Console.WriteLine($"Unique Terms: {stats.TermCount:N0}");
                    Console.WriteLine($"Total Postings: {stats.PostingCount:N0}");
                    Console.WriteLine($"Avg Doc Length: {stats.AverageDocumentLength:F1} tokens");
                    Console.WriteLine($"Total Document Size: {stats.TotalDocumentSize:N0} bytes");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error retrieving statistics: {ex.Message}");
            }
        }

        private void HandleDebugCommand(string[] parts)
        {
            if (parts.Length < 2)
            {
                Console.WriteLine("Usage: debug <term> [--lemmatizer] [--stopwords]");
                return;
            }

            string term = parts[1];
            bool showLemmatizer = parts.Any(p => p.ToLowerInvariant() == "--lemmatizer");
            bool showStopwords = parts.Any(p => p.ToLowerInvariant() == "--stopwords");
            bool showAll = !showLemmatizer && !showStopwords;

            Console.WriteLine($"=== Debug: {term} ===");

            if (showAll || showLemmatizer)
            {
                BasicLemmatizer lemmatizer = new BasicLemmatizer();
                string lemmatized = lemmatizer.Lemmatize(term);
                Console.WriteLine($"Lemmatized: {term} -> {lemmatized}");
            }

            if (showAll || showStopwords)
            {
                BasicStopWordRemover stopWords = new BasicStopWordRemover();
                bool isStopWord = stopWords.IsStopWord(term);
                Console.WriteLine($"Stop Word: {(isStopWord ? "Yes (would be filtered)" : "No")}");
            }
        }

        #endregion

        #region Maintenance Commands

        private async Task HandleFlushCommandAsync()
        {
            if (_IndexManager.CurrentIndex == null)
            {
                Console.WriteLine("No index initialized. Use 'index create' first.");
                return;
            }

            if (_IndexManager.CurrentIndex.Configuration?.StorageMode == StorageMode.InMemory)
            {
                Console.WriteLine("In-memory indices don't require flushing.");
                return;
            }

            try
            {
                await _IndexManager.CurrentIndex.FlushAsync(null, CancellationToken.None).ConfigureAwait(false);
                Console.WriteLine("Flushed pending operations.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error flushing: {ex.Message}");
            }
        }

        #endregion

        #region Testing Commands

        private async Task HandleDemoCommandAsync()
        {
            if (_IndexManager.CurrentIndex == null)
            {
                Console.WriteLine("No index initialized. Use 'index create' first.");
                return;
            }

            Console.WriteLine("Loading demo data...");

            string[] sampleDocuments = {
                "The quick brown fox jumps over the lazy dog. This pangram contains all letters.",
                "Machine learning algorithms process vast amounts of data to identify patterns.",
                "Climate change affects weather patterns and ecosystems around the world.",
                "Software development requires careful planning and continuous improvement.",
                "The human brain is a complex network that processes information."
            };

            try
            {
                for (int i = 0; i < sampleDocuments.Length; i++)
                {
                    string docName = $"demo{i + 1}";
                    string docId = await _IndexManager.CurrentIndex.AddDocumentAsync($"{docName}.txt", sampleDocuments[i]).ConfigureAwait(false);
                    _IndexManager.CurrentDocumentNames[docName] = docId;
                }

                Console.WriteLine($"Added {sampleDocuments.Length} demo documents.");
                Console.WriteLine("Try: search \"machine learning\" or search \"fox dog\"");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task HandleBenchmarkCommandAsync()
        {
            if (_IndexManager.CurrentIndex == null)
            {
                Console.WriteLine("No index initialized. Use 'index create' first.");
                return;
            }

            Console.WriteLine("=== Benchmark ===");

            try
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

                // Add 100 documents
                for (int i = 0; i < 100; i++)
                {
                    string content = $"Benchmark document {i} with content about data analysis, machine learning, software development.";
                    string docId = await _IndexManager.CurrentIndex.AddDocumentAsync($"bench{i}.txt", content).ConfigureAwait(false);
                    _IndexManager.CurrentDocumentNames[$"bench{i}"] = docId;
                }

                double addTime = sw.Elapsed.TotalMilliseconds;

                // Search
                sw.Restart();
                string[] terms = { "benchmark", "machine", "learning", "data", "software" };
                foreach (string term in terms)
                {
                    await _IndexManager.CurrentIndex.SearchAsync(term, null, false, CancellationToken.None).ConfigureAwait(false);
                }
                double searchTime = sw.Elapsed.TotalMilliseconds;

                Console.WriteLine($"Added 100 docs: {addTime:F1}ms ({addTime/100:F2}ms each)");
                Console.WriteLine($"5 searches: {searchTime:F1}ms ({searchTime/5:F2}ms each)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task HandleStressCommandAsync()
        {
            if (_IndexManager.CurrentIndex == null)
            {
                Console.WriteLine("No index initialized. Use 'index create' first.");
                return;
            }

            Console.WriteLine("=== Stress Test (1000 docs) ===");

            string[] words = { "data", "analysis", "machine", "learning", "algorithm", "database", "software", "development" };
            Random random = new Random();

            try
            {
                System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

                for (int i = 0; i < 1000; i++)
                {
                    List<string> contentWords = new List<string>();
                    for (int j = 0; j < random.Next(20, 50); j++)
                    {
                        contentWords.Add(words[random.Next(words.Length)]);
                    }

                    string content = string.Join(" ", contentWords);
                    string docId = await _IndexManager.CurrentIndex.AddDocumentAsync($"stress{i}.txt", content).ConfigureAwait(false);
                    _IndexManager.CurrentDocumentNames[$"stress{i}"] = docId;

                    if ((i + 1) % 200 == 0)
                    {
                        Console.WriteLine($"  {i + 1}/1000 documents...");
                    }
                }

                Console.WriteLine($"Completed in {sw.Elapsed.TotalSeconds:F1}s");

                IndexStatistics stats = await _IndexManager.CurrentIndex.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);
                Console.WriteLine($"Total: {stats.DocumentCount} docs, {stats.TermCount} terms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        private async Task HandleExportCommandAsync(string[] parts)
        {
            if (_IndexManager.CurrentIndex == null)
            {
                Console.WriteLine("No index initialized. Use 'index create' first.");
                return;
            }

            if (parts.Length < 2)
            {
                Console.WriteLine("Usage: export <filename>");
                return;
            }

            string fileName = parts[1];

            try
            {
                IndexStatistics stats = await _IndexManager.CurrentIndex.GetStatisticsAsync(CancellationToken.None).ConfigureAwait(false);

                Dictionary<string, object> exportData = new Dictionary<string, object>
                {
                    ["timestamp"] = DateTime.UtcNow,
                    ["documents"] = stats.DocumentCount,
                    ["terms"] = stats.TermCount
                };

                string json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(fileName, json, CancellationToken.None).ConfigureAwait(false);

                Console.WriteLine($"Exported to {fileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }

        #endregion

        #region Helpers

        private string? GetDocumentName(string docId)
        {
            return _IndexManager.CurrentDocumentNames.FirstOrDefault(kvp => kvp.Value == docId).Key;
        }

        #endregion
    }
}
