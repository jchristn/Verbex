namespace VerbexCli.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Manages multiple Verbex indices for the CLI
    /// </summary>
    public sealed class IndexManager : IDisposable
    {
        private static IndexManager? _Instance;
        private readonly Dictionary<string, InvertedIndex> _LoadedIndices = new Dictionary<string, InvertedIndex>();
        private readonly Dictionary<string, IndexConfiguration> _Configurations = new Dictionary<string, IndexConfiguration>();
        private readonly Dictionary<string, Dictionary<string, string>> _DocumentMaps = new Dictionary<string, Dictionary<string, string>>();
        private string? _CurrentIndexName;
        private readonly string _ConfigDirectory;

        /// <summary>
        /// Gets the singleton instance of the IndexManager
        /// </summary>
        public static IndexManager Instance => _Instance ??= new IndexManager();

        /// <summary>
        /// Initializes the singleton instance with a custom configuration directory
        /// This must be called before accessing Instance for the first time
        /// </summary>
        /// <param name="configDirectory">Custom configuration directory path</param>
        public static void Initialize(string configDirectory)
        {
            if (_Instance != null)
            {
                throw new InvalidOperationException("IndexManager has already been initialized");
            }
            _Instance = new IndexManager(configDirectory);
        }

        /// <summary>
        /// Initializes a new instance of the IndexManager class
        /// </summary>
        private IndexManager()
        {
            _ConfigDirectory = GlobalConfig.GetEffectiveConfigDirectory();
            Directory.CreateDirectory(_ConfigDirectory);
            LoadPersistedConfigurations();
        }

        /// <summary>
        /// Initializes a new instance of the IndexManager class with a custom config directory
        /// </summary>
        /// <param name="configDirectory">Custom configuration directory path</param>
        private IndexManager(string configDirectory)
        {
            _ConfigDirectory = configDirectory;
            Directory.CreateDirectory(_ConfigDirectory);
            LoadPersistedConfigurations();
        }

        /// <summary>
        /// Gets the name of the current active index
        /// </summary>
        public string? CurrentIndexName => _CurrentIndexName;

        /// <summary>
        /// Gets the current active index
        /// </summary>
        public InvertedIndex? CurrentIndex => _CurrentIndexName != null && _LoadedIndices.ContainsKey(_CurrentIndexName)
            ? _LoadedIndices[_CurrentIndexName]
            : null;

        /// <summary>
        /// Gets all available index configurations
        /// </summary>
        public IReadOnlyDictionary<string, IndexConfiguration> Configurations => _Configurations;

        /// <summary>
        /// Creates a new index with the specified configuration
        /// </summary>
        public async Task CreateIndexAsync(string name, string storageMode, bool enableLemmatizer, bool enableStopWords,
            int minTokenLength, int maxTokenLength, CancellationToken cancellationToken = default)
        {
            await CreateIndexAsync(name, storageMode, enableLemmatizer, enableStopWords, minTokenLength, maxTokenLength, null, null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Creates a new index with the specified configuration including tags and labels
        /// </summary>
        public async Task CreateIndexAsync(string name, string storageMode, bool enableLemmatizer, bool enableStopWords,
            int minTokenLength, int maxTokenLength, Dictionary<string, string>? tags, List<string>? labels, CancellationToken cancellationToken = default)
        {
            if (_Configurations.ContainsKey(name))
            {
                throw new InvalidOperationException($"Index '{name}' already exists");
            }

            // Map old storage modes to new ones
            StorageMode mode = storageMode.ToLowerInvariant() switch
            {
                "memory" => StorageMode.InMemory,
                "disk" => StorageMode.OnDisk,
                "hybrid" => StorageMode.OnDisk,  // Hybrid maps to OnDisk
                "persistence" => StorageMode.OnDisk,
                "persistenceonly" => StorageMode.OnDisk,
                _ => throw new ArgumentException($"Invalid storage mode: {storageMode}")
            };

            VerbexConfiguration config = new VerbexConfiguration
            {
                StorageMode = mode,
                MinTokenLength = minTokenLength > 0 ? minTokenLength : 0,
                MaxTokenLength = maxTokenLength > 0 ? maxTokenLength : 0
            };

            if (enableLemmatizer)
            {
                config.Lemmatizer = new BasicLemmatizer();
            }

            if (enableStopWords)
            {
                config.StopWordRemover = new BasicStopWordRemover();
            }

            // Set storage directory for on-disk mode
            if (mode == StorageMode.OnDisk)
            {
                config.StorageDirectory = Path.Combine(_ConfigDirectory, "indices", name);
            }

            InvertedIndex index = new InvertedIndex(name, config);
            await index.OpenAsync(cancellationToken).ConfigureAwait(false);

            IndexConfiguration indexConfig = new IndexConfiguration
            {
                Name = name,
                Description = BuildDescription(mode, enableLemmatizer, enableStopWords, minTokenLength, maxTokenLength),
                VerbexConfig = config,
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };

            _Configurations[name] = indexConfig;
            _LoadedIndices[name] = index;
            _DocumentMaps[name] = new Dictionary<string, string>();

            // Save configuration
            await SaveConfigurationAsync(name, indexConfig, cancellationToken).ConfigureAwait(false);
            await SavePersistedConfigurationsAsync(cancellationToken).ConfigureAwait(false);

            // Set as current if it's the first index
            if (_CurrentIndexName == null)
            {
                _CurrentIndexName = name;
            }
        }

        /// <summary>
        /// Lists all available indices
        /// </summary>
        public object[] ListIndices()
        {
            return _Configurations.Values.Select(config =>
            {
                if (!_DocumentMaps.ContainsKey(config.Name))
                {
                    try
                    {
                        LoadDocumentMapAsync(config.Name, CancellationToken.None).Wait(TimeSpan.FromSeconds(2));
                    }
                    catch
                    {
                        // Ignore errors loading document map
                    }
                }

                int documentCount = _DocumentMaps.ContainsKey(config.Name) ? _DocumentMaps[config.Name].Count : 0;

                return new
                {
                    Name = config.Name,
                    Storage = config.VerbexConfig.StorageMode.ToString().ToLowerInvariant(),
                    Documents = documentCount,
                    Status = config.Name == _CurrentIndexName ? "active" : "inactive"
                };
            }).ToArray();
        }

        /// <summary>
        /// Switches to the specified index
        /// </summary>
        public async Task UseIndexAsync(string name, CancellationToken cancellationToken = default)
        {
            if (!_Configurations.ContainsKey(name))
            {
                throw new ArgumentException($"Index '{name}' does not exist");
            }

            if (!_LoadedIndices.ContainsKey(name))
            {
                await LoadIndexAsync(name, cancellationToken).ConfigureAwait(false);
            }

            _CurrentIndexName = name;
            _Configurations[name].LastAccessedAt = DateTime.UtcNow;

            await SavePersistedConfigurationsAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Deletes an index
        /// </summary>
        public async Task DeleteIndexAsync(string name, CancellationToken cancellationToken = default)
        {
            if (!_Configurations.ContainsKey(name))
            {
                throw new ArgumentException($"Index '{name}' does not exist");
            }

            if (_LoadedIndices.ContainsKey(name))
            {
                if (_LoadedIndices[name].Configuration?.StorageMode == StorageMode.OnDisk)
                {
                    await _LoadedIndices[name].FlushAsync(null, cancellationToken).ConfigureAwait(false);
                }
                await _LoadedIndices[name].DisposeAsync().ConfigureAwait(false);
                _LoadedIndices.Remove(name);
            }

            IndexConfiguration config = _Configurations[name];
            _Configurations.Remove(name);
            _DocumentMaps.Remove(name);

            // Delete persistent data if exists
            if (config.VerbexConfig.StorageMode == StorageMode.OnDisk &&
                !string.IsNullOrEmpty(config.VerbexConfig.StorageDirectory) &&
                Directory.Exists(config.VerbexConfig.StorageDirectory))
            {
                Directory.Delete(config.VerbexConfig.StorageDirectory, true);
            }

            if (_CurrentIndexName == name)
            {
                _CurrentIndexName = _Configurations.Keys.FirstOrDefault();
            }

            await SavePersistedConfigurationsAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets information about an index
        /// </summary>
        public async Task<object> GetIndexInfoAsync(string? name = null, CancellationToken cancellationToken = default)
        {
            string indexName = name ?? _CurrentIndexName ?? throw new InvalidOperationException("No index specified and no current index");

            if (!_Configurations.ContainsKey(indexName))
            {
                throw new ArgumentException($"Index '{indexName}' does not exist");
            }

            IndexConfiguration config = _Configurations[indexName];
            InvertedIndex? index = _LoadedIndices.ContainsKey(indexName) ? _LoadedIndices[indexName] : null;

            long docCount = 0;
            long termCount = 0;

            if (index != null)
            {
                docCount = await index.GetDocumentCountAsync(cancellationToken).ConfigureAwait(false);
                IndexStatistics stats = await index.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);
                termCount = stats.TermCount;
            }

            return new
            {
                Name = indexName,
                Storage = config.VerbexConfig.StorageMode.ToString().ToLowerInvariant(),
                Documents = docCount,
                Terms = termCount,
                Created = config.CreatedAt,
                LastModified = config.LastAccessedAt,
                IsLoaded = index != null,
                Configuration = new
                {
                    Lemmatizer = config.VerbexConfig.Lemmatizer != null,
                    StopWords = config.VerbexConfig.StopWordRemover != null,
                    MinTokenLength = config.VerbexConfig.MinTokenLength,
                    MaxTokenLength = config.VerbexConfig.MaxTokenLength,
                    StorageDirectory = config.VerbexConfig.StorageDirectory
                }
            };
        }

        /// <summary>
        /// Adds a document to the specified index
        /// </summary>
        public async Task AddDocumentAsync(string indexName, string documentName, string content, CancellationToken cancellationToken = default)
        {
            await AddDocumentAsync(indexName, documentName, content, null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a document to the specified index with optional metadata
        /// </summary>
        public async Task AddDocumentAsync(string indexName, string documentName, string content, Dictionary<string, object>? metadata, CancellationToken cancellationToken = default)
        {
            await AddDocumentAsync(indexName, documentName, content, metadata, null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a document to the specified index with optional metadata and labels
        /// </summary>
        public async Task AddDocumentAsync(string indexName, string documentName, string content, Dictionary<string, object>? metadata, List<string>? labels, CancellationToken cancellationToken = default)
        {
            InvertedIndex index = await GetIndexAsync(indexName, cancellationToken).ConfigureAwait(false);

            if (_DocumentMaps[indexName].ContainsKey(documentName))
            {
                throw new InvalidOperationException($"Document '{documentName}' already exists in index '{indexName}'");
            }

            string docId = await index.AddDocumentAsync($"{documentName}.txt", content, cancellationToken).ConfigureAwait(false);
            _DocumentMaps[indexName][documentName] = docId;

            // Add labels if provided
            if (labels != null)
            {
                foreach (string label in labels)
                {
                    await index.AddLabelAsync(docId, label, cancellationToken).ConfigureAwait(false);
                }
            }

            // Add tags if provided
            if (metadata != null)
            {
                foreach (KeyValuePair<string, object> kvp in metadata)
                {
                    await index.SetTagAsync(docId, kvp.Key, kvp.Value?.ToString(), cancellationToken).ConfigureAwait(false);
                }
            }

            // Flush on-disk indices
            if (index.Configuration?.StorageMode == StorageMode.OnDisk)
            {
                await index.FlushAsync(null, cancellationToken).ConfigureAwait(false);
            }

            await SaveDocumentContentAsync(indexName, documentName, content, cancellationToken).ConfigureAwait(false);
            await SaveDocumentMapAsync(indexName, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a document from a file to the specified index
        /// </summary>
        public async Task AddDocumentFromFileAsync(string indexName, string documentName, string filePath, CancellationToken cancellationToken = default)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"File not found: {filePath}");
            }

            string content = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
            await AddDocumentAsync(indexName, documentName, content, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Removes a document from the specified index
        /// </summary>
        public async Task RemoveDocumentAsync(string indexName, string documentName, CancellationToken cancellationToken = default)
        {
            InvertedIndex index = await GetIndexAsync(indexName, cancellationToken).ConfigureAwait(false);

            if (!_DocumentMaps[indexName].TryGetValue(documentName, out string? docId) || docId == null)
            {
                throw new ArgumentException($"Document '{documentName}' not found in index '{indexName}'");
            }

            bool removed = await index.RemoveDocumentAsync(docId, cancellationToken).ConfigureAwait(false);
            if (removed)
            {
                _DocumentMaps[indexName].Remove(documentName);

                if (index.Configuration?.StorageMode == StorageMode.OnDisk)
                {
                    await index.FlushAsync(null, cancellationToken).ConfigureAwait(false);
                }

                await SaveDocumentMapAsync(indexName, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Lists documents in the specified index
        /// </summary>
        public async Task<object[]> ListDocumentsAsync(string indexName, CancellationToken cancellationToken = default)
        {
            InvertedIndex index = await GetIndexAsync(indexName, cancellationToken).ConfigureAwait(false);

            List<object> documents = new List<object>();
            foreach (KeyValuePair<string, string> kvp in _DocumentMaps[indexName])
            {
                try
                {
                    DocumentMetadata? metadata = await index.GetDocumentAsync(kvp.Value, cancellationToken).ConfigureAwait(false);
                    if (metadata != null)
                    {
                        documents.Add(new
                        {
                            Name = kvp.Key,
                            Size = $"{metadata.DocumentLength} chars",
                            Terms = metadata.Terms.Count,
                            Added = metadata.IndexedDate
                        });
                    }
                }
                catch
                {
                    documents.Add(new
                    {
                        Name = kvp.Key,
                        Size = "Unknown",
                        Terms = 0,
                        Added = DateTime.MinValue
                    });
                }
            }

            return documents.ToArray();
        }

        /// <summary>
        /// Searches documents in the specified index
        /// </summary>
        public async Task<object[]> SearchAsync(string indexName, string query, bool useAndLogic, int limit, CancellationToken cancellationToken = default)
        {
            return await SearchAsync(indexName, query, useAndLogic, limit, null, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Searches documents in the specified index with optional metadata filters
        /// </summary>
        public async Task<object[]> SearchAsync(string indexName, string query, bool useAndLogic, int limit, Dictionary<string, object>? metadataFilters, CancellationToken cancellationToken = default)
        {
            InvertedIndex index = await GetIndexAsync(indexName, cancellationToken).ConfigureAwait(false);

            // Note: Label/tag filtering is not currently supported in the new API
            SearchResults results = await index.SearchAsync(query, limit, useAndLogic, cancellationToken).ConfigureAwait(false);

            return results.Results.Select(result => new
            {
                Document = GetDocumentName(indexName, result.DocumentId) ?? "Unknown",
                Score = Math.Round(result.Score, 4),
                MatchedTerms = result.MatchedTermCount
            }).ToArray();
        }

        /// <summary>
        /// Gets statistics for the specified index
        /// </summary>
        public async Task<object> GetStatisticsAsync(string indexName, CancellationToken cancellationToken = default)
        {
            InvertedIndex index = await GetIndexAsync(indexName, cancellationToken).ConfigureAwait(false);

            IndexStatistics stats = await index.GetStatisticsAsync(cancellationToken).ConfigureAwait(false);

            return new
            {
                Index = indexName,
                Documents = stats.DocumentCount,
                Terms = stats.TermCount,
                Postings = stats.PostingCount,
                AverageDocumentLength = Math.Round(stats.AverageDocumentLength, 2),
                TotalSize = $"{stats.TotalDocumentSize:N0} chars"
            };
        }

        /// <summary>
        /// Flushes the specified index
        /// </summary>
        public async Task FlushAsync(string indexName, CancellationToken cancellationToken = default)
        {
            InvertedIndex index = await GetIndexAsync(indexName, cancellationToken).ConfigureAwait(false);

            if (index.Configuration?.StorageMode == StorageMode.OnDisk)
            {
                await index.FlushAsync(null, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Disposes all resources
        /// </summary>
        public void Dispose()
        {
            foreach (InvertedIndex index in _LoadedIndices.Values)
            {
                try
                {
                    if (index.Configuration?.StorageMode == StorageMode.OnDisk)
                    {
                        index.FlushAsync(null, CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
                    }
                    index.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            _LoadedIndices.Clear();
        }

        // Private helper methods

        private void LoadPersistedConfigurations()
        {
            string configFile = Path.Combine(_ConfigDirectory, "cli-config.json");

            if (File.Exists(configFile))
            {
                try
                {
                    string json = File.ReadAllText(configFile);
                    PersistedConfiguration? persistedConfig = JsonSerializer.Deserialize<PersistedConfiguration>(json);
                    if (persistedConfig != null)
                    {
                        _CurrentIndexName = persistedConfig.CurrentIndex;

                        foreach (SerializableIndexConfiguration serializableConfig in persistedConfig.Indices)
                        {
                            IndexConfiguration indexConfig = new IndexConfiguration
                            {
                                Name = serializableConfig.Name,
                                Description = serializableConfig.Description,
                                CreatedAt = serializableConfig.CreatedAt,
                                LastAccessedAt = serializableConfig.LastAccessedAt,
                                VerbexConfig = new VerbexConfiguration
                                {
                                    StorageMode = serializableConfig.VerbexConfig.StorageMode,
                                    MinTokenLength = serializableConfig.VerbexConfig.MinTokenLength,
                                    MaxTokenLength = serializableConfig.VerbexConfig.MaxTokenLength,
                                    StorageDirectory = serializableConfig.VerbexConfig.StorageDirectory
                                }
                            };

                            if (serializableConfig.VerbexConfig.HasLemmatizer)
                            {
                                indexConfig.VerbexConfig.Lemmatizer = new BasicLemmatizer();
                            }

                            if (serializableConfig.VerbexConfig.HasStopWordRemover)
                            {
                                indexConfig.VerbexConfig.StopWordRemover = new BasicStopWordRemover();
                            }

                            _Configurations[indexConfig.Name] = indexConfig;
                            _DocumentMaps[indexConfig.Name] = new Dictionary<string, string>();

                            try
                            {
                                LoadDocumentMapAsync(indexConfig.Name, CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
                            }
                            catch
                            {
                                // Ignore loading errors
                            }
                        }
                        return;
                    }
                }
                catch
                {
                    // Fall back to default if loading fails
                }
            }

            InitializeDefaultConfigurations();
        }

        private void InitializeDefaultConfigurations()
        {
            IndexConfiguration defaultConfig = new IndexConfiguration
            {
                Name = "default",
                Description = "Default in-memory index",
                VerbexConfig = new VerbexConfiguration { StorageMode = StorageMode.InMemory },
                CreatedAt = DateTime.UtcNow,
                LastAccessedAt = DateTime.UtcNow
            };

            _Configurations["default"] = defaultConfig;
            _DocumentMaps["default"] = new Dictionary<string, string>();
            _CurrentIndexName = "default";

            SavePersistedConfigurationsAsync(CancellationToken.None).Wait(TimeSpan.FromSeconds(5));
        }

        private async Task SavePersistedConfigurationsAsync(CancellationToken cancellationToken)
        {
            try
            {
                PersistedConfiguration persistedConfig = new PersistedConfiguration
                {
                    CurrentIndex = _CurrentIndexName,
                    Indices = _Configurations.Values.Select(config => new SerializableIndexConfiguration
                    {
                        Name = config.Name,
                        Description = config.Description,
                        CreatedAt = config.CreatedAt,
                        LastAccessedAt = config.LastAccessedAt,
                        VerbexConfig = new SerializableVerbexConfiguration
                        {
                            StorageMode = config.VerbexConfig.StorageMode,
                            MinTokenLength = config.VerbexConfig.MinTokenLength,
                            MaxTokenLength = config.VerbexConfig.MaxTokenLength,
                            HasLemmatizer = config.VerbexConfig.Lemmatizer != null,
                            HasStopWordRemover = config.VerbexConfig.StopWordRemover != null,
                            StorageDirectory = config.VerbexConfig.StorageDirectory
                        }
                    }).ToList()
                };

                string configFile = Path.Combine(_ConfigDirectory, "cli-config.json");
                string json = JsonSerializer.Serialize(persistedConfig, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(configFile, json, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to save configuration: {ex.Message}");
                throw;
            }
        }

        private async Task<InvertedIndex> GetIndexAsync(string indexName, CancellationToken cancellationToken)
        {
            if (!_Configurations.ContainsKey(indexName))
            {
                throw new ArgumentException($"Index '{indexName}' does not exist");
            }

            if (!_LoadedIndices.ContainsKey(indexName))
            {
                await LoadIndexAsync(indexName, cancellationToken).ConfigureAwait(false);
            }

            return _LoadedIndices[indexName];
        }

        private async Task LoadIndexAsync(string indexName, CancellationToken cancellationToken)
        {
            IndexConfiguration config = _Configurations[indexName];

            InvertedIndex index = new InvertedIndex(indexName, config.VerbexConfig);
            await index.OpenAsync(cancellationToken).ConfigureAwait(false);

            _LoadedIndices[indexName] = index;

            if (!_DocumentMaps.ContainsKey(indexName))
            {
                _DocumentMaps[indexName] = new Dictionary<string, string>();
            }

            await LoadDocumentMapAsync(indexName, cancellationToken).ConfigureAwait(false);

            // Reload documents from saved content for in-memory indices
            if (config.VerbexConfig.StorageMode == StorageMode.InMemory)
            {
                await ReloadDocumentsAsync(indexName, index, cancellationToken).ConfigureAwait(false);
            }
        }

        private async Task ReloadDocumentsAsync(string indexName, InvertedIndex index, CancellationToken cancellationToken)
        {
            if (!_DocumentMaps.ContainsKey(indexName) || _DocumentMaps[indexName].Count == 0)
                return;

            IndexConfiguration config = _Configurations[indexName];

            string contentDir;
            if (!string.IsNullOrEmpty(config.VerbexConfig.StorageDirectory))
            {
                contentDir = Path.Combine(config.VerbexConfig.StorageDirectory, "documents");
            }
            else
            {
                contentDir = Path.Combine(_ConfigDirectory, "document-content", indexName);
            }

            if (!Directory.Exists(contentDir))
                return;

            Dictionary<string, string> newDocMap = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kvp in _DocumentMaps[indexName])
            {
                string documentName = kvp.Key;
                string contentFile = Path.Combine(contentDir, $"{documentName}.txt");

                if (File.Exists(contentFile))
                {
                    try
                    {
                        string content = await File.ReadAllTextAsync(contentFile, cancellationToken).ConfigureAwait(false);
                        string newDocId = await index.AddDocumentAsync($"{documentName}.txt", content, cancellationToken).ConfigureAwait(false);
                        newDocMap[documentName] = newDocId;
                    }
                    catch
                    {
                        // Skip documents that fail to load
                    }
                }
            }

            _DocumentMaps[indexName] = newDocMap;
        }

        private async Task SaveConfigurationAsync(string indexName, IndexConfiguration config, CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(config.VerbexConfig.StorageDirectory)) return;

            object saveConfig = new
            {
                config.Description,
                StorageMode = config.VerbexConfig.StorageMode,
                MinTokenLength = config.VerbexConfig.MinTokenLength,
                MaxTokenLength = config.VerbexConfig.MaxTokenLength,
                HasLemmatizer = config.VerbexConfig.Lemmatizer != null,
                HasStopWordRemover = config.VerbexConfig.StopWordRemover != null,
                config.CreatedAt,
                config.LastAccessedAt
            };

            Directory.CreateDirectory(config.VerbexConfig.StorageDirectory);
            string configFile = Path.Combine(config.VerbexConfig.StorageDirectory, "index-config.json");
            string json = JsonSerializer.Serialize(saveConfig, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(configFile, json, cancellationToken).ConfigureAwait(false);
        }

        private async Task LoadDocumentMapAsync(string indexName, CancellationToken cancellationToken)
        {
            IndexConfiguration config = _Configurations[indexName];

            string docMapFile;
            if (!string.IsNullOrEmpty(config.VerbexConfig.StorageDirectory))
            {
                docMapFile = Path.Combine(config.VerbexConfig.StorageDirectory, "document-map.json");
            }
            else
            {
                string indexDir = Path.Combine(_ConfigDirectory, "document-maps");
                docMapFile = Path.Combine(indexDir, $"{indexName}-documents.json");
            }

            if (File.Exists(docMapFile))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(docMapFile, cancellationToken).ConfigureAwait(false);
                    Dictionary<string, string>? docMap = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (docMap != null)
                    {
                        _DocumentMaps[indexName] = docMap;
                    }
                }
                catch
                {
                    _DocumentMaps[indexName] = new Dictionary<string, string>();
                }
            }
        }

        private async Task SaveDocumentContentAsync(string indexName, string documentName, string content, CancellationToken cancellationToken)
        {
            IndexConfiguration config = _Configurations[indexName];

            string contentDir;
            if (!string.IsNullOrEmpty(config.VerbexConfig.StorageDirectory))
            {
                contentDir = Path.Combine(config.VerbexConfig.StorageDirectory, "documents");
            }
            else
            {
                contentDir = Path.Combine(_ConfigDirectory, "document-content", indexName);
            }

            Directory.CreateDirectory(contentDir);
            string contentFile = Path.Combine(contentDir, $"{documentName}.txt");
            await File.WriteAllTextAsync(contentFile, content, cancellationToken).ConfigureAwait(false);
        }

        private async Task SaveDocumentMapAsync(string indexName, CancellationToken cancellationToken)
        {
            IndexConfiguration config = _Configurations[indexName];

            string docMapFile;
            if (!string.IsNullOrEmpty(config.VerbexConfig.StorageDirectory))
            {
                docMapFile = Path.Combine(config.VerbexConfig.StorageDirectory, "document-map.json");
            }
            else
            {
                string indexDir = Path.Combine(_ConfigDirectory, "document-maps");
                Directory.CreateDirectory(indexDir);
                docMapFile = Path.Combine(indexDir, $"{indexName}-documents.json");
            }

            string json = JsonSerializer.Serialize(_DocumentMaps[indexName], new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(docMapFile, json, cancellationToken).ConfigureAwait(false);
        }

        private string? GetDocumentName(string indexName, string docId)
        {
            return _DocumentMaps[indexName].FirstOrDefault(kvp => kvp.Value == docId).Key;
        }

        private static string BuildDescription(StorageMode mode, bool lemmatizer, bool stopWords, int minLength, int maxLength)
        {
            List<string> features = new List<string> { mode.ToString() };
            if (lemmatizer) features.Add("lemmatizer");
            if (stopWords) features.Add("stop-words");
            if (minLength > 0) features.Add($"min-len:{minLength}");
            if (maxLength > 0) features.Add($"max-len:{maxLength}");
            return string.Join(", ", features);
        }
    }
}
