namespace Verbex.Server.Services
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Verbex;
    using Verbex.Database;
    using Verbex.Models;

    /// <summary>
    /// Manages multiple inverted indices with database-backed metadata storage.
    /// </summary>
    public class IndexManager
    {
        #region Private-Members

        private readonly ConcurrentDictionary<string, InvertedIndex> _Indices;
        private readonly ConcurrentDictionary<string, IndexMetadata> _Metadata;
        private readonly string _Header = "[IndexManager] ";
        private readonly LoggingModule? _Logging;
        private readonly DatabaseDriverBase _Database;
        private string _DataDirectory = "./data";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="database">Database driver for metadata storage.</param>
        /// <param name="logging">Logging module.</param>
        /// <exception cref="ArgumentNullException">Thrown when database is null.</exception>
        public IndexManager(DatabaseDriverBase database, LoggingModule? logging = null)
        {
            _Database = database ?? throw new ArgumentNullException(nameof(database));
            _Logging = logging;
            _Indices = new ConcurrentDictionary<string, InvertedIndex>();
            _Metadata = new ConcurrentDictionary<string, IndexMetadata>();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Discover and initialize indices from the database for a specific tenant.
        /// </summary>
        /// <param name="tenantId">The tenant ID to load indices for.</param>
        /// <param name="dataDirectory">The root data directory for index storage.</param>
        /// <param name="token">Cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown when tenantId or dataDirectory is null or empty.</exception>
        public async Task DiscoverIndicesAsync(string tenantId, string dataDirectory, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(dataDirectory)) throw new ArgumentNullException(nameof(dataDirectory));

            _DataDirectory = dataDirectory;

            if (!Directory.Exists(_DataDirectory))
            {
                Directory.CreateDirectory(_DataDirectory);
                _Logging?.Info(_Header + "created data directory: " + _DataDirectory);
            }

            List<IndexMetadata> indices = await _Database.Indexes.ReadManyAsync(tenantId, limit: 1000, token: token).ConfigureAwait(false);
            _Logging?.Info(_Header + "found " + indices.Count + " indices for tenant '" + tenantId + "'");

            foreach (IndexMetadata metadata in indices)
            {
                if (!metadata.Enabled)
                {
                    _Logging?.Info(_Header + "skipping disabled index '" + metadata.Identifier + "'");
                    continue;
                }

                if (_Indices.ContainsKey(metadata.Identifier))
                {
                    _Logging?.Info(_Header + "index '" + metadata.Identifier + "' already loaded, skipping");
                    continue;
                }

                try
                {
                    await InitializeIndexAsync(metadata, token).ConfigureAwait(false);
                    _Logging?.Info(_Header + "initialized index '" + metadata.Identifier + "' (" + metadata.Name + ")");
                }
                catch (Exception ex)
                {
                    _Logging?.Error(_Header + "failed to initialize index '" + metadata.Identifier + "': " + ex.Message);
                }
            }

            _Logging?.Info(_Header + "index discovery complete for tenant '" + tenantId + "', " + _Indices.Count + " indices loaded");
        }

        /// <summary>
        /// Get all index metadata.
        /// </summary>
        /// <returns>List of index metadata.</returns>
        public List<IndexMetadata> GetAllMetadata()
        {
            return _Metadata.Values.ToList();
        }

        /// <summary>
        /// Get all index metadata for a specific tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <returns>List of index metadata for the tenant.</returns>
        public List<IndexMetadata> GetAllMetadata(string tenantId)
        {
            if (String.IsNullOrEmpty(tenantId)) return new List<IndexMetadata>();
            return _Metadata.Values.Where(m => m.TenantId == tenantId).ToList();
        }

        /// <summary>
        /// Get index metadata by identifier.
        /// </summary>
        /// <param name="indexId">Index identifier.</param>
        /// <returns>Index metadata or null if not found.</returns>
        public IndexMetadata? GetMetadata(string indexId)
        {
            if (String.IsNullOrEmpty(indexId)) return null;
            _Metadata.TryGetValue(indexId, out IndexMetadata? metadata);
            return metadata;
        }

        /// <summary>
        /// Get inverted index by identifier.
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
        /// Check if index exists by name within a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="name">Index name.</param>
        /// <returns>True if index exists, false otherwise.</returns>
        public bool IndexExistsByName(string tenantId, string name)
        {
            if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(name)) return false;
            return _Metadata.Values.Any(m => m.TenantId == tenantId && m.Name == name);
        }

        /// <summary>
        /// Create a new index.
        /// </summary>
        /// <param name="metadata">Index metadata.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The created index metadata.</returns>
        /// <exception cref="ArgumentNullException">Thrown when metadata is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when index already exists.</exception>
        public async Task<IndexMetadata> CreateIndexAsync(IndexMetadata metadata, CancellationToken token = default)
        {
            if (metadata == null) throw new ArgumentNullException(nameof(metadata));

            if (_Indices.ContainsKey(metadata.Identifier))
            {
                throw new InvalidOperationException("Index with identifier '" + metadata.Identifier + "' already exists");
            }

            if (IndexExistsByName(metadata.TenantId, metadata.Name))
            {
                throw new InvalidOperationException("Index with name '" + metadata.Name + "' already exists in tenant '" + metadata.TenantId + "'");
            }

            // Create in database first
            IndexMetadata created = await _Database.Indexes.CreateAsync(metadata, token).ConfigureAwait(false);

            // Initialize the runtime index
            await InitializeIndexAsync(created, token).ConfigureAwait(false);

            _Logging?.Info(_Header + "created index '" + created.Identifier + "' (" + created.Name + ") for tenant '" + created.TenantId + "'");

            return created;
        }

        /// <summary>
        /// Delete an index.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True if deleted successfully, false otherwise.</returns>
        public async Task<bool> DeleteIndexAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(indexId)) return false;

            // Delete from database
            bool deleted = await _Database.Indexes.DeleteByIdentifierAsync(tenantId, indexId, token).ConfigureAwait(false);

            if (deleted)
            {
                // Remove from runtime
                bool removedIndex = _Indices.TryRemove(indexId, out InvertedIndex? index);
                _Metadata.TryRemove(indexId, out _);

                if (removedIndex && index != null)
                {
                    await index.DisposeAsync().ConfigureAwait(false);
                }

                _Logging?.Info(_Header + "deleted index '" + indexId + "'");
            }

            return deleted;
        }

        /// <summary>
        /// Get index statistics.
        /// </summary>
        /// <param name="indexId">Index identifier.</param>
        /// <returns>Index statistics or null if index not found.</returns>
        public async Task<object?> GetIndexStatisticsAsync(string indexId)
        {
            InvertedIndex? index = GetIndex(indexId);
            IndexMetadata? metadata = GetMetadata(indexId);

            if (index == null || metadata == null) return null;

            return new
            {
                Identifier = metadata.Identifier,
                TenantId = metadata.TenantId,
                Name = metadata.Name,
                Description = metadata.Description,
                Enabled = metadata.Enabled,
                InMemory = metadata.InMemory,
                CreatedUtc = metadata.CreatedUtc,
                LastUpdateUtc = metadata.LastUpdateUtc,
                Labels = metadata.Labels,
                Tags = metadata.Tags,
                Statistics = await index.GetStatisticsAsync().ConfigureAwait(false)
            };
        }

        /// <summary>
        /// Update index labels (full replacement).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="labels">New labels list.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated metadata if successful, null if index not found.</returns>
        public async Task<IndexMetadata?> UpdateIndexLabelsAsync(string tenantId, string indexId, List<string> labels, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(indexId)) return null;
            if (!_Metadata.TryGetValue(indexId, out IndexMetadata? metadata)) return null;

            metadata.Labels = labels ?? new List<string>();
            metadata.LastUpdateUtc = DateTime.UtcNow;

            IndexMetadata updated = await _Database.Indexes.UpdateAsync(metadata, token).ConfigureAwait(false);
            _Metadata[indexId] = updated;

            _Logging?.Info(_Header + "updated labels for index '" + indexId + "'");
            return updated;
        }

        /// <summary>
        /// Update index tags (full replacement).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="tags">New tags dictionary.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated metadata if successful, null if index not found.</returns>
        public async Task<IndexMetadata?> UpdateIndexTagsAsync(string tenantId, string indexId, Dictionary<string, string> tags, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(indexId)) return null;
            if (!_Metadata.TryGetValue(indexId, out IndexMetadata? metadata)) return null;

            metadata.Tags = tags ?? new Dictionary<string, string>();
            metadata.LastUpdateUtc = DateTime.UtcNow;

            IndexMetadata updated = await _Database.Indexes.UpdateAsync(metadata, token).ConfigureAwait(false);
            _Metadata[indexId] = updated;

            _Logging?.Info(_Header + "updated tags for index '" + indexId + "'");
            return updated;
        }

        /// <summary>
        /// Update index custom metadata (full replacement).
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="indexId">Index identifier.</param>
        /// <param name="customMetadata">New custom metadata (can be any JSON-serializable value or null).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Updated metadata if successful, null if index not found.</returns>
        public async Task<IndexMetadata?> UpdateIndexCustomMetadataAsync(string tenantId, string indexId, object? customMetadata, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId) || String.IsNullOrEmpty(indexId)) return null;
            if (!_Metadata.TryGetValue(indexId, out IndexMetadata? metadata)) return null;

            metadata.CustomMetadata = customMetadata;
            metadata.LastUpdateUtc = DateTime.UtcNow;

            IndexMetadata updated = await _Database.Indexes.UpdateAsync(metadata, token).ConfigureAwait(false);
            _Metadata[indexId] = updated;

            _Logging?.Info(_Header + "updated custom metadata for index '" + indexId + "'");
            return updated;
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
            _Metadata.Clear();
        }

        #endregion

        #region Private-Methods

        /// <summary>
        /// Initialize a single index from its metadata.
        /// </summary>
        /// <param name="metadata">Index metadata.</param>
        /// <param name="token">Cancellation token.</param>
        private async Task InitializeIndexAsync(IndexMetadata metadata, CancellationToken token = default)
        {
            StorageMode storageMode = metadata.InMemory ? StorageMode.InMemory : StorageMode.OnDisk;

            VerbexConfiguration verbexConfig = new VerbexConfiguration
            {
                StorageMode = storageMode,
                MinTokenLength = metadata.MinTokenLength,
                MaxTokenLength = metadata.MaxTokenLength,
                Lemmatizer = metadata.EnableLemmatizer ? new BasicLemmatizer() : null,
                StopWordRemover = metadata.EnableStopWordRemover ? new BasicStopWordRemover() : null
            };

            if (storageMode == StorageMode.OnDisk)
            {
                string indexDirectory = Path.Combine(_DataDirectory, metadata.TenantId, metadata.Identifier);
                if (!Directory.Exists(indexDirectory))
                {
                    Directory.CreateDirectory(indexDirectory);
                }
                verbexConfig.StorageDirectory = indexDirectory;
            }

            InvertedIndex index = new InvertedIndex(metadata.Identifier, verbexConfig);
            await index.OpenAsync().ConfigureAwait(false);

            _Indices[metadata.Identifier] = index;
            _Metadata[metadata.Identifier] = metadata;
        }

        #endregion
    }
}
