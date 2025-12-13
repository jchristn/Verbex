namespace TestConsole
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Manages multiple indices, their configurations, and persistence operations.
    /// </summary>
    public class IndexManager
    {
        private readonly Dictionary<string, IndexConfiguration> _AvailableConfigurations = new Dictionary<string, IndexConfiguration>();
        private readonly Dictionary<string, InvertedIndex> _LoadedIndices = new Dictionary<string, InvertedIndex>();
        private readonly Dictionary<string, Dictionary<string, string>> _IndexDocumentMaps = new Dictionary<string, Dictionary<string, string>>();
        private string _CurrentIndexName = "memory";

        /// <summary>
        /// Gets the current active index.
        /// </summary>
        public InvertedIndex? CurrentIndex { get; private set; }

        /// <summary>
        /// Gets the current index name.
        /// </summary>
        public string CurrentIndexName => _CurrentIndexName;

        /// <summary>
        /// Gets the current storage mode display name.
        /// </summary>
        public string CurrentStorageMode { get; private set; } = "InMemory";

        /// <summary>
        /// Gets the document names for the current index.
        /// </summary>
        public Dictionary<string, string> CurrentDocumentNames { get; private set; } = new Dictionary<string, string>();

        /// <summary>
        /// Gets all available configurations.
        /// </summary>
        public IReadOnlyDictionary<string, IndexConfiguration> AvailableConfigurations => _AvailableConfigurations;

        /// <summary>
        /// Initializes default index configurations.
        /// </summary>
        public void InitializeDefaultConfigurations()
        {
            // Default in-memory configuration
            _AvailableConfigurations["memory"] = new IndexConfiguration
            {
                Name = "memory",
                Description = "In-memory storage with no text processing",
                VerbexConfig = new VerbexConfiguration
                {
                    StorageMode = StorageMode.InMemory
                }
            };

            // Default on-disk configuration
            _AvailableConfigurations["disk"] = new IndexConfiguration
            {
                Name = "disk",
                Description = "On-disk storage with no text processing",
                VerbexConfig = new VerbexConfiguration
                {
                    StorageMode = StorageMode.OnDisk,
                    StorageDirectory = Path.Combine(Path.GetTempPath(), "VerbexConsole", "disk")
                }
            };

            // Lemmatized configuration
            _AvailableConfigurations["lemmatized"] = new IndexConfiguration
            {
                Name = "lemmatized",
                Description = "In-memory storage with lemmatization enabled",
                VerbexConfig = new VerbexConfiguration
                {
                    StorageMode = StorageMode.InMemory,
                    Lemmatizer = new BasicLemmatizer()
                }
            };

            // Stop words configuration
            _AvailableConfigurations["nostopwords"] = new IndexConfiguration
            {
                Name = "nostopwords",
                Description = "In-memory storage with stop word removal",
                VerbexConfig = new VerbexConfiguration
                {
                    StorageMode = StorageMode.InMemory,
                    StopWordRemover = new BasicStopWordRemover()
                }
            };

            // Full text processing configuration
            _AvailableConfigurations["fulltext"] = new IndexConfiguration
            {
                Name = "fulltext",
                Description = "On-disk storage with full text processing (lemmatizer + stop words + token length)",
                VerbexConfig = new VerbexConfiguration
                {
                    StorageMode = StorageMode.OnDisk,
                    StorageDirectory = Path.Combine(Path.GetTempPath(), "VerbexConsole", "fulltext"),
                    Lemmatizer = new BasicLemmatizer(),
                    StopWordRemover = new BasicStopWordRemover(),
                    MinTokenLength = 3,
                    MaxTokenLength = 50
                }
            };

            // Initialize document maps for all configurations
            foreach (string configName in _AvailableConfigurations.Keys)
            {
                _IndexDocumentMaps[configName] = new Dictionary<string, string>();
            }

            // Start with memory configuration
            _CurrentIndexName = "memory";
            CurrentDocumentNames = _IndexDocumentMaps["memory"];
        }

        /// <summary>
        /// Discovers existing persistent indices from disk.
        /// </summary>
        public async Task DiscoverExistingIndicesAsync()
        {
            try
            {
                string baseDir = Path.Combine(Path.GetTempPath(), "VerbexConsole");
                if (!Directory.Exists(baseDir))
                {
                    return;
                }

                Console.WriteLine("Discovering existing persistent indices...");

                foreach (string indexDir in Directory.GetDirectories(baseDir))
                {
                    string configFile = Path.Combine(indexDir, "index-config.json");
                    if (File.Exists(configFile))
                    {
                        try
                        {
                            string configJson = await File.ReadAllTextAsync(configFile).ConfigureAwait(false);
                            SavedIndexConfiguration? savedConfig = JsonSerializer.Deserialize<SavedIndexConfiguration>(configJson);

                            if (savedConfig != null)
                            {
                                string indexName = Path.GetFileName(indexDir);

                                // Create configuration from saved data
                                IndexConfiguration config = new IndexConfiguration
                                {
                                    Name = indexName,
                                    Description = $"[PERSISTENT] {savedConfig.Description}",
                                    IsPersistent = true,
                                    CreatedAt = savedConfig.CreatedAt,
                                    LastAccessedAt = savedConfig.LastAccessedAt,
                                    VerbexConfig = new VerbexConfiguration
                                    {
                                        StorageMode = savedConfig.StorageMode,
                                        StorageDirectory = indexDir,
                                        MinTokenLength = savedConfig.MinTokenLength,
                                        MaxTokenLength = savedConfig.MaxTokenLength
                                    }
                                };

                                // Restore text processing components
                                if (savedConfig.HasLemmatizer)
                                {
                                    config.VerbexConfig.Lemmatizer = new BasicLemmatizer();
                                }

                                if (savedConfig.HasStopWordRemover)
                                {
                                    config.VerbexConfig.StopWordRemover = new BasicStopWordRemover();
                                }

                                // Add to available configurations
                                _AvailableConfigurations[indexName] = config;

                                // Initialize document map
                                _IndexDocumentMaps[indexName] = new Dictionary<string, string>();

                                Console.WriteLine($"  Found persistent index: {indexName}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"  Error loading index config from {indexDir}: {ex.Message}");
                        }
                    }
                }

                Console.WriteLine("Index discovery completed.");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during index discovery: {ex.Message}");
            }
        }

        /// <summary>
        /// Switches to a different index configuration.
        /// </summary>
        /// <param name="targetConfig">Name of the target configuration.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if switch was successful.</returns>
        public async Task<bool> SwitchToIndexAsync(string targetConfig, CancellationToken cancellationToken = default)
        {
            if (!_AvailableConfigurations.ContainsKey(targetConfig))
            {
                Console.WriteLine($"Configuration '{targetConfig}' not found.");
                Console.WriteLine("Use 'list-configs' to see available configurations.");
                return false;
            }

            // Save current state
            if (CurrentIndex != null)
            {
                // Only flush on-disk indices
                if (CurrentIndex.Configuration?.StorageMode == StorageMode.OnDisk)
                {
                    await CurrentIndex.FlushAsync(null, cancellationToken).ConfigureAwait(false);
                }

                // Store current index and document map
                _LoadedIndices[_CurrentIndexName] = CurrentIndex;
                _IndexDocumentMaps[_CurrentIndexName] = new Dictionary<string, string>(CurrentDocumentNames);
            }

            // Switch to target configuration
            _CurrentIndexName = targetConfig;
            IndexConfiguration config = _AvailableConfigurations[targetConfig];

            // Load or create index
            if (_LoadedIndices.ContainsKey(targetConfig))
            {
                CurrentIndex = _LoadedIndices[targetConfig];
                Console.WriteLine($"Switched to existing index '{targetConfig}'.");
            }
            else
            {
                try
                {
                    CurrentIndex = new InvertedIndex(config.Name, config.VerbexConfig);
                    await CurrentIndex.OpenAsync(cancellationToken).ConfigureAwait(false);
                    Console.WriteLine($"Created and switched to new index '{targetConfig}'.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error creating index '{targetConfig}': {ex.Message}");
                    // Fallback to memory configuration
                    _CurrentIndexName = "memory";
                    CurrentIndex = _LoadedIndices.ContainsKey("memory") ? _LoadedIndices["memory"] : null;
                    return false;
                }
            }

            // Restore document map
            CurrentDocumentNames.Clear();
            if (_IndexDocumentMaps.ContainsKey(targetConfig))
            {
                foreach (KeyValuePair<string, string> kvp in _IndexDocumentMaps[targetConfig])
                {
                    CurrentDocumentNames[kvp.Key] = kvp.Value;
                }
            }

            // Update current storage mode for display
            CurrentStorageMode = $"{targetConfig}-{config.VerbexConfig.StorageMode}";

            Console.WriteLine($"Active configuration: {config.Description}");
            Console.WriteLine($"Documents available: {CurrentDocumentNames.Count}");
            return true;
        }

        /// <summary>
        /// Creates a new index configuration.
        /// </summary>
        /// <param name="name">Configuration name.</param>
        /// <param name="storageMode">Storage mode.</param>
        /// <param name="enableLemmatizer">Whether to enable lemmatizer.</param>
        /// <param name="enableStopwords">Whether to enable stop word removal.</param>
        /// <param name="minLength">Minimum token length.</param>
        /// <param name="maxLength">Maximum token length.</param>
        /// <returns>True if configuration was created successfully.</returns>
        public bool CreateConfiguration(string name, StorageMode storageMode, bool enableLemmatizer, bool enableStopwords, int minLength, int maxLength)
        {
            try
            {
                // Create configuration
                VerbexConfiguration config = new VerbexConfiguration
                {
                    StorageMode = storageMode,
                    MinTokenLength = minLength,
                    MaxTokenLength = maxLength
                };

                if (enableLemmatizer)
                {
                    config.Lemmatizer = new BasicLemmatizer();
                }

                if (enableStopwords)
                {
                    config.StopWordRemover = new BasicStopWordRemover();
                }

                if (storageMode == StorageMode.OnDisk)
                {
                    config.StorageDirectory = Path.Combine(Path.GetTempPath(), "VerbexConsole", name);
                }

                // Build description
                List<string> features = new List<string>();
                features.Add(storageMode.ToString());
                if (enableLemmatizer) features.Add("lemmatizer");
                if (enableStopwords) features.Add("stop words");
                if (minLength > 0) features.Add($"min-length:{minLength}");
                if (maxLength > 0) features.Add($"max-length:{maxLength}");

                string description = $"Custom configuration: {string.Join(", ", features)}";

                // Store configuration
                _AvailableConfigurations[name] = new IndexConfiguration
                {
                    Name = name,
                    Description = description,
                    VerbexConfig = config
                };

                // Initialize document map if needed
                if (!_IndexDocumentMaps.ContainsKey(name))
                {
                    _IndexDocumentMaps[name] = new Dictionary<string, string>();
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating configuration '{name}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves the current index configuration to disk.
        /// </summary>
        /// <param name="indexName">Name for the saved index.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if save was successful.</returns>
        public async Task<bool> SaveCurrentIndexAsync(string indexName, CancellationToken cancellationToken = default)
        {
            if (CurrentIndex == null)
            {
                Console.WriteLine("No active index to save.");
                return false;
            }

            try
            {
                // Create storage directory
                string storageDir = Path.Combine(Path.GetTempPath(), "VerbexConsole", indexName);
                Directory.CreateDirectory(storageDir);

                // Flush current index if on-disk
                if (CurrentIndex.Configuration?.StorageMode == StorageMode.OnDisk)
                {
                    await CurrentIndex.FlushAsync(null, cancellationToken).ConfigureAwait(false);
                }

                // If current index is in-memory, we need to create a new disk-backed version
                VerbexConfiguration diskConfig;
                if (CurrentIndex.Configuration?.StorageMode == StorageMode.InMemory)
                {
                    // Create a new disk-backed configuration
                    diskConfig = new VerbexConfiguration
                    {
                        StorageMode = StorageMode.OnDisk,
                        StorageDirectory = storageDir,
                        MinTokenLength = CurrentIndex.Configuration.MinTokenLength,
                        MaxTokenLength = CurrentIndex.Configuration.MaxTokenLength,
                        Lemmatizer = CurrentIndex.Configuration.Lemmatizer,
                        StopWordRemover = CurrentIndex.Configuration.StopWordRemover
                    };

                    // Create new disk-backed index
                    InvertedIndex diskIndex = new InvertedIndex(indexName, diskConfig);
                    await diskIndex.OpenAsync(cancellationToken).ConfigureAwait(false);

                    Console.WriteLine($"Converting in-memory index to on-disk index '{indexName}'...");
                    Console.WriteLine("Note: Document content will need to be re-added to the new persistent index.");

                    // Store in our tracking
                    _LoadedIndices[indexName] = diskIndex;
                }
                else
                {
                    // Index is already on-disk, just update its storage directory if needed
                    diskConfig = CurrentIndex.Configuration!;
                    if (diskConfig.StorageDirectory != storageDir)
                    {
                        diskConfig.StorageDirectory = storageDir;
                    }
                }

                // Save index configuration
                IndexConfiguration indexConfig = new IndexConfiguration
                {
                    Name = indexName,
                    Description = $"Saved index: {diskConfig.StorageMode}",
                    IsPersistent = true,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessedAt = DateTime.UtcNow,
                    VerbexConfig = diskConfig
                };

                await SaveIndexConfigurationAsync(indexName, indexConfig).ConfigureAwait(false);

                // Save document map
                await SaveDocumentMapAsync(indexName, CurrentDocumentNames).ConfigureAwait(false);

                // Add to available configurations
                _AvailableConfigurations[indexName] = indexConfig;
                _IndexDocumentMaps[indexName] = new Dictionary<string, string>(CurrentDocumentNames);

                Console.WriteLine($"Successfully saved index '{indexName}' to disk");
                Console.WriteLine($"Storage directory: {storageDir}");
                Console.WriteLine($"Documents: {CurrentDocumentNames.Count}");
                Console.WriteLine();
                Console.WriteLine("This index will be available after restart using 'reload-index {0}' or 'discover-indices'.", indexName);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving index '{indexName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Reloads a persistent index from disk.
        /// </summary>
        /// <param name="indexName">Name of the index to reload.</param>
        /// <returns>True if reload was successful.</returns>
        public async Task<bool> ReloadIndexAsync(string indexName)
        {
            if (!_AvailableConfigurations.ContainsKey(indexName))
            {
                Console.WriteLine($"Index configuration '{indexName}' not found.");
                Console.WriteLine("Use 'discover-indices' to scan for available persistent indices.");
                return false;
            }

            IndexConfiguration config = _AvailableConfigurations[indexName];

            if (!config.IsPersistent)
            {
                Console.WriteLine($"Index '{indexName}' is not a persistent index.");
                return false;
            }

            try
            {
                // Check if index directory exists
                if (!Directory.Exists(config.VerbexConfig.StorageDirectory))
                {
                    Console.WriteLine($"Index directory not found: {config.VerbexConfig.StorageDirectory}");
                    return false;
                }

                // Load the index
                InvertedIndex index = new InvertedIndex(indexName, config.VerbexConfig);
                await index.OpenAsync().ConfigureAwait(false);

                // Update last accessed time
                config.LastAccessedAt = DateTime.UtcNow;
                await SaveIndexConfigurationAsync(indexName, config).ConfigureAwait(false);

                // Load document map if it exists
                string docMapFile = Path.Combine(config.VerbexConfig.StorageDirectory!, "document-map.json");
                if (File.Exists(docMapFile))
                {
                    try
                    {
                        string docMapJson = await File.ReadAllTextAsync(docMapFile).ConfigureAwait(false);
                        Dictionary<string, string>? docMap = JsonSerializer.Deserialize<Dictionary<string, string>>(docMapJson);
                        if (docMap != null)
                        {
                            _IndexDocumentMaps[indexName] = docMap;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Warning: Could not load document map: {ex.Message}");
                        _IndexDocumentMaps[indexName] = new Dictionary<string, string>();
                    }
                }

                long docCount = await index.GetDocumentCountAsync().ConfigureAwait(false);

                Console.WriteLine($"Successfully reloaded persistent index '{indexName}'");
                Console.WriteLine($"Documents: {docCount}");
                Console.WriteLine($"Document names: {_IndexDocumentMaps[indexName].Count}");
                Console.WriteLine($"Storage directory: {config.VerbexConfig.StorageDirectory}");
                Console.WriteLine();
                Console.WriteLine("Use 'switch-index {0}' to activate this index.", indexName);

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reloading index '{indexName}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a description of token length settings.
        /// </summary>
        /// <param name="config">Configuration to describe.</param>
        /// <returns>Token length description.</returns>
        public static string GetTokenLengthDescription(VerbexConfiguration config)
        {
            List<string> parts = new List<string>();

            if (config.MinTokenLength > 0)
                parts.Add($"min: {config.MinTokenLength}");

            if (config.MaxTokenLength > 0)
                parts.Add($"max: {config.MaxTokenLength}");

            return parts.Count > 0 ? string.Join(", ", parts) : "No limits";
        }

        /// <summary>
        /// Cleans up all loaded indices.
        /// </summary>
        public async Task CleanupAsync()
        {
            try
            {
                // Flush current index if it exists and is on-disk
                if (CurrentIndex != null && CurrentIndex.Configuration?.StorageMode == StorageMode.OnDisk)
                {
                    await CurrentIndex.FlushAsync(null, CancellationToken.None).ConfigureAwait(false);
                }

                // Clean up all loaded indices
                foreach (KeyValuePair<string, InvertedIndex> kvp in _LoadedIndices)
                {
                    try
                    {
                        if (kvp.Value.Configuration?.StorageMode == StorageMode.OnDisk)
                        {
                            await kvp.Value.FlushAsync(null, CancellationToken.None).ConfigureAwait(false);
                        }
                        await kvp.Value.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error disposing index '{kvp.Key}': {ex.Message}");
                    }
                }

                if (CurrentIndex != null && !_LoadedIndices.ContainsValue(CurrentIndex))
                {
                    await CurrentIndex.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Saves index configuration to disk.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="config">Configuration to save.</param>
        private async Task SaveIndexConfigurationAsync(string indexName, IndexConfiguration config)
        {
            if (config.VerbexConfig.StorageDirectory == null)
            {
                throw new InvalidOperationException("Storage directory not set for persistent index");
            }

            Directory.CreateDirectory(config.VerbexConfig.StorageDirectory);

            SavedIndexConfiguration savedConfig = new SavedIndexConfiguration
            {
                Description = config.Description,
                StorageMode = config.VerbexConfig.StorageMode,
                MinTokenLength = config.VerbexConfig.MinTokenLength,
                MaxTokenLength = config.VerbexConfig.MaxTokenLength,
                HasLemmatizer = config.VerbexConfig.Lemmatizer != null,
                HasStopWordRemover = config.VerbexConfig.StopWordRemover != null,
                CreatedAt = config.CreatedAt,
                LastAccessedAt = config.LastAccessedAt
            };

            string configFile = Path.Combine(config.VerbexConfig.StorageDirectory, "index-config.json");
            string configJson = JsonSerializer.Serialize(savedConfig, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(configFile, configJson).ConfigureAwait(false);
        }

        /// <summary>
        /// Saves document map to disk.
        /// </summary>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="documentMap">Document map to save.</param>
        private async Task SaveDocumentMapAsync(string indexName, Dictionary<string, string> documentMap)
        {
            if (!_AvailableConfigurations.ContainsKey(indexName) ||
                _AvailableConfigurations[indexName].VerbexConfig.StorageDirectory == null)
            {
                return;
            }

            string storageDir = _AvailableConfigurations[indexName].VerbexConfig.StorageDirectory!;
            string docMapFile = Path.Combine(storageDir, "document-map.json");

            string docMapJson = JsonSerializer.Serialize(documentMap, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(docMapFile, docMapJson).ConfigureAwait(false);
        }
    }
}
