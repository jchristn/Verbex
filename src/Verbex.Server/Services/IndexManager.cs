namespace Verbex.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Verbex;
    using Verbex.Server.Classes;

    /// <summary>
    /// Manages multiple inverted indices.
    /// </summary>
    public class IndexManager
    {
        #region Public-Members

        /// <summary>
        /// The name of the index metadata file stored in each index directory.
        /// </summary>
        public const string IndexMetadataFilename = "index.json";

        #endregion

        #region Private-Members

        private readonly ConcurrentDictionary<string, InvertedIndex> _Indices;
        private readonly ConcurrentDictionary<string, IndexConfiguration> _Configurations;
        private readonly string _Header = "[IndexManager] ";
        private LoggingModule? _Logging = null;
        private string _DataDirectory = "./data";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="logging">Logging module.</param>
        public IndexManager(LoggingModule? logging = null)
        {
            _Logging = logging;
            _Indices = new ConcurrentDictionary<string, InvertedIndex>();
            _Configurations = new ConcurrentDictionary<string, IndexConfiguration>();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Discover and initialize indices from the data directory.
        /// Scans for subdirectories containing index.json metadata files.
        /// </summary>
        /// <param name="dataDirectory">The root data directory to scan for indices.</param>
        /// <exception cref="ArgumentNullException">Thrown when dataDirectory is null or empty.</exception>
        public async Task DiscoverIndicesAsync(string dataDirectory)
        {
            if (String.IsNullOrEmpty(dataDirectory)) throw new ArgumentNullException(nameof(dataDirectory));

            _DataDirectory = dataDirectory;

            if (!Directory.Exists(_DataDirectory))
            {
                Directory.CreateDirectory(_DataDirectory);
                _Logging?.Info(_Header + "created data directory: " + _DataDirectory);
                return;
            }

            string[] indexDirectories = Directory.GetDirectories(_DataDirectory);
            _Logging?.Info(_Header + "scanning data directory '" + _DataDirectory + "', found " + indexDirectories.Length + " subdirectories");

            foreach (string indexDir in indexDirectories)
            {
                string metadataPath = Path.Combine(indexDir, IndexMetadataFilename);

                IndexConfiguration? config = IndexConfiguration.FromFile(metadataPath);
                if (config == null)
                {
                    _Logging?.Warn(_Header + "skipping directory '" + indexDir + "': no valid " + IndexMetadataFilename + " found");
                    continue;
                }

                if (!config.Enabled)
                {
                    _Logging?.Info(_Header + "skipping disabled index '" + config.Id + "'");
                    continue;
                }

                try
                {
                    await InitializeIndexAsync(config, indexDir).ConfigureAwait(false);
                    _Logging?.Info(_Header + "discovered and initialized index '" + config.Id + "' (" + config.Name + ")");
                }
                catch (Exception ex)
                {
                    _Logging?.Error(_Header + "failed to initialize discovered index '" + config.Id + "': " + ex.Message);
                }
            }

            _Logging?.Info(_Header + "index discovery complete, " + _Indices.Count + " indices loaded");
        }

        /// <summary>
        /// Initialize a single index from its configuration.
        /// </summary>
        /// <param name="config">Index configuration.</param>
        /// <param name="indexDirectory">Directory path for the index data.</param>
        /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
        private async Task InitializeIndexAsync(IndexConfiguration config, string indexDirectory)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            StorageMode storageMode = ParseStorageMode(config);

            VerbexConfiguration verbexConfig = new VerbexConfiguration
            {
                StorageMode = storageMode,
                MinTokenLength = config.MinTokenLength,
                MaxTokenLength = config.MaxTokenLength,
                Lemmatizer = config.EnableLemmatizer ? new BasicLemmatizer() : null,
                StopWordRemover = config.EnableStopWordRemover ? new BasicStopWordRemover() : null
            };

            if (storageMode == StorageMode.OnDisk)
            {
                verbexConfig.StorageDirectory = indexDirectory;
            }

            InvertedIndex index = new InvertedIndex(config.Id, verbexConfig);
            await index.OpenAsync().ConfigureAwait(false);

            _Indices[config.Id] = index;
            _Configurations[config.Id] = config;
        }

        /// <summary>
        /// Parse storage mode from configuration.
        /// </summary>
        /// <param name="config">Index configuration.</param>
        /// <returns>Storage mode.</returns>
        private static StorageMode ParseStorageMode(IndexConfiguration config)
        {
            if (config.InMemory ||
                config.StorageMode.Equals("MemoryOnly", StringComparison.OrdinalIgnoreCase) ||
                config.StorageMode.Equals("Memory", StringComparison.OrdinalIgnoreCase) ||
                config.StorageMode.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
            {
                return StorageMode.InMemory;
            }

            // All other modes map to OnDisk (PersistenceOnly, Hybrid, Disk, etc.)
            return StorageMode.OnDisk;
        }

        /// <summary>
        /// Get all index configurations.
        /// </summary>
        /// <returns>List of index configurations.</returns>
        public List<IndexConfiguration> GetAllConfigurations()
        {
            return _Configurations.Values.ToList();
        }

        /// <summary>
        /// Get index configuration by ID.
        /// </summary>
        /// <param name="indexId">Index identifier.</param>
        /// <returns>Index configuration or null if not found.</returns>
        public IndexConfiguration? GetConfiguration(string indexId)
        {
            if (String.IsNullOrEmpty(indexId)) return null;
            _Configurations.TryGetValue(indexId, out IndexConfiguration? config);
            return config;
        }

        /// <summary>
        /// Get inverted index by ID.
        /// </summary>
        /// <param name="indexId">Index identifier.</param>
        /// <returns>Inverted index or null if not found.</returns>
        public InvertedIndex? GetIndex(string indexId)
        {
            if (String.IsNullOrEmpty(indexId)) return null;
            _Indices.TryGetValue(indexId, out InvertedIndex? index);
            return index;
        }

        /// <summary>
        /// Check if index exists.
        /// </summary>
        /// <param name="indexId">Index identifier.</param>
        /// <returns>True if index exists, false otherwise.</returns>
        public bool IndexExists(string indexId)
        {
            if (String.IsNullOrEmpty(indexId)) return false;
            return _Indices.ContainsKey(indexId);
        }

        /// <summary>
        /// Create a new index.
        /// </summary>
        /// <param name="config">Index configuration.</param>
        /// <returns>True if created successfully, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown when config is null.</exception>
        /// <exception cref="ArgumentException">Thrown when index ID is empty.</exception>
        public async Task<bool> CreateIndexAsync(IndexConfiguration config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (String.IsNullOrEmpty(config.Id)) throw new ArgumentException("Index ID cannot be empty");

            if (_Indices.ContainsKey(config.Id))
            {
                return false; // Index already exists
            }

            try
            {
                StorageMode storageMode = ParseStorageMode(config);
                string indexDirectory = Path.Combine(_DataDirectory, config.Id);

                VerbexConfiguration verbexConfig = new VerbexConfiguration
                {
                    StorageMode = storageMode,
                    MinTokenLength = config.MinTokenLength,
                    MaxTokenLength = config.MaxTokenLength,
                    Lemmatizer = config.EnableLemmatizer ? new BasicLemmatizer() : null,
                    StopWordRemover = config.EnableStopWordRemover ? new BasicStopWordRemover() : null
                };

                if (storageMode == StorageMode.InMemory)
                {
                    // Memory-only indices do not persist to disk, so no directory or index.json is created.
                    // These indices will not survive server restarts.
                    _Logging?.Debug(_Header + "memory-only index '" + config.Id + "' will not be persisted to disk");
                }
                else
                {
                    // Persistent indices require a directory and index.json for discovery on restart
                    verbexConfig.StorageDirectory = indexDirectory;

                    // Save index metadata to index.json for discovery on restart
                    Directory.CreateDirectory(indexDirectory);
                    string metadataPath = Path.Combine(indexDirectory, IndexMetadataFilename);
                    config.ToFile(metadataPath);
                }

                InvertedIndex index = new InvertedIndex(config.Id, verbexConfig);
                await index.OpenAsync().ConfigureAwait(false);

                _Indices[config.Id] = index;
                _Configurations[config.Id] = config;

                _Logging?.Info(_Header + "created index '" + config.Id + "' (" + config.Name + ")");
                return true;
            }
            catch (Exception ex)
            {
                _Logging?.Error(_Header + "failed to create index '" + config.Id + "': " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Delete an index.
        /// </summary>
        /// <param name="indexId">Index identifier.</param>
        /// <returns>True if deleted successfully, false otherwise.</returns>
        public async Task<bool> DeleteIndexAsync(string indexId)
        {
            if (String.IsNullOrEmpty(indexId)) return false;

            bool removed = _Indices.TryRemove(indexId, out InvertedIndex? index);
            if (removed)
            {
                _Configurations.TryRemove(indexId, out IndexConfiguration? config);
                if (index != null)
                {
                    await index.DisposeAsync().ConfigureAwait(false);
                }
                _Logging?.Info(_Header + "deleted index '" + indexId + "'");
            }

            return removed;
        }

        /// <summary>
        /// Get index statistics.
        /// </summary>
        /// <param name="indexId">Index identifier.</param>
        /// <returns>Index statistics or null if index not found.</returns>
        public async Task<object?> GetIndexStatisticsAsync(string indexId)
        {
            InvertedIndex? index = GetIndex(indexId);
            IndexConfiguration? config = GetConfiguration(indexId);

            if (index == null || config == null) return null;

            return new
            {
                Id = config.Id,
                Name = config.Name,
                Description = config.Description,
                Enabled = config.Enabled,
                InMemory = config.InMemory,
                CreatedUtc = config.CreatedUtc,
                Labels = config.Labels,
                Tags = config.Tags,
                Statistics = await index.GetStatisticsAsync().ConfigureAwait(false)
            };
        }

        /// <summary>
        /// Update index labels (full replacement).
        /// </summary>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="labels">New labels list.</param>
        /// <returns>True if updated successfully, false if index not found.</returns>
        public bool UpdateIndexLabels(string indexId, List<string> labels)
        {
            if (String.IsNullOrEmpty(indexId)) return false;
            if (!_Configurations.TryGetValue(indexId, out IndexConfiguration? config)) return false;

            config.Labels = labels ?? new List<string>();

            // Persist to index.json if not memory-only
            if (!config.InMemory && !config.StorageMode.Equals("MemoryOnly", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string indexDirectory = Path.Combine(_DataDirectory, indexId);
                    string metadataPath = Path.Combine(indexDirectory, IndexMetadataFilename);
                    if (Directory.Exists(indexDirectory))
                    {
                        config.ToFile(metadataPath);
                    }
                }
                catch (Exception ex)
                {
                    _Logging?.Warn(_Header + "failed to persist labels update for index '" + indexId + "': " + ex.Message);
                }
            }

            _Logging?.Info(_Header + "updated labels for index '" + indexId + "'");
            return true;
        }

        /// <summary>
        /// Update index tags (full replacement).
        /// </summary>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="tags">New tags dictionary.</param>
        /// <returns>True if updated successfully, false if index not found.</returns>
        public bool UpdateIndexTags(string indexId, Dictionary<string, string> tags)
        {
            if (String.IsNullOrEmpty(indexId)) return false;
            if (!_Configurations.TryGetValue(indexId, out IndexConfiguration? config)) return false;

            config.Tags = tags ?? new Dictionary<string, string>();

            // Persist to index.json if not memory-only
            if (!config.InMemory && !config.StorageMode.Equals("MemoryOnly", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string indexDirectory = Path.Combine(_DataDirectory, indexId);
                    string metadataPath = Path.Combine(indexDirectory, IndexMetadataFilename);
                    if (Directory.Exists(indexDirectory))
                    {
                        config.ToFile(metadataPath);
                    }
                }
                catch (Exception ex)
                {
                    _Logging?.Warn(_Header + "failed to persist tags update for index '" + indexId + "': " + ex.Message);
                }
            }

            _Logging?.Info(_Header + "updated tags for index '" + indexId + "'");
            return true;
        }

        /// <summary>
        /// Dispose all indices and clean up resources.
        /// </summary>
        public async Task DisposeAllAsync()
        {
            foreach (KeyValuePair<string, InvertedIndex> kvp in _Indices)
            {
                try
                {
                    await kvp.Value.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _Logging?.Warn(_Header + "error disposing index '" + kvp.Key + "': " + ex.Message);
                }
            }
            _Indices.Clear();
            _Configurations.Clear();
        }

        #endregion

        #region Private-Methods

        #endregion
    }
}
