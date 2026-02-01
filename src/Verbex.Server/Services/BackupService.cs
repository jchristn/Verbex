namespace Verbex.Server.Services
{
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using SyslogLogging;
    using Verbex;
    using Verbex.Models;
    using Verbex.Server.Classes;

    /// <summary>
    /// Service for backup and restore operations on indices.
    /// </summary>
    public class BackupService : IBackupService
    {
        #region Private-Members

        private static readonly JsonSerializerOptions _JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private const string ManifestFileName = "manifest.json";
        private const string MetadataFileName = "metadata.json";
        private const string IndexDbFileName = "index.db";
        private const string CurrentVerbexVersion = "0.1.8";
        private const string SupportedManifestVersion = "1.0";

        private readonly IndexManager _IndexManager;
        private readonly string _DataDirectory;
        private readonly LoggingModule? _Logging;
        private readonly string _Header = "[BackupService] ";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the BackupService class.
        /// </summary>
        /// <param name="indexManager">The index manager.</param>
        /// <param name="dataDirectory">The root data directory for index storage.</param>
        /// <param name="logging">Optional logging module.</param>
        /// <exception cref="ArgumentNullException">Thrown when indexManager or dataDirectory is null.</exception>
        public BackupService(IndexManager indexManager, string dataDirectory, LoggingModule? logging = null)
        {
            _IndexManager = indexManager ?? throw new ArgumentNullException(nameof(indexManager));
            _DataDirectory = dataDirectory ?? throw new ArgumentNullException(nameof(dataDirectory));
            _Logging = logging;
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public async Task<Stream> CreateBackupAsync(string tenantId, string indexId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(indexId)) throw new ArgumentNullException(nameof(indexId));

            InvertedIndex? index = _IndexManager.GetIndex(indexId);
            IndexMetadata? metadata = _IndexManager.GetMetadata(indexId);

            if (index == null || metadata == null)
            {
                throw new InvalidOperationException("Index not found: " + indexId);
            }

            _Logging?.Info(_Header + "starting backup for index '" + indexId + "' (InMemory=" + metadata.InMemory + ")");

            string indexDbPath;
            string? tempFilePath = null;

            if (metadata.InMemory)
            {
                // For in-memory indices, save to a temporary file first
                tempFilePath = Path.Combine(Path.GetTempPath(), $"verbex_backup_{indexId}_{Guid.NewGuid():N}.db");
                Verbex.Database.Sqlite.SqliteDatabaseDriver driver = (Verbex.Database.Sqlite.SqliteDatabaseDriver)index.Driver;
                await driver.SaveToFileAsync(tempFilePath, token).ConfigureAwait(false);
                indexDbPath = tempFilePath;
                _Logging?.Debug(_Header + "saved in-memory index to temp file: " + tempFilePath);
            }
            else
            {
                string indexDirectory = Path.Combine(_DataDirectory, tenantId, indexId);
                indexDbPath = Path.Combine(indexDirectory, IndexDbFileName);

                if (!File.Exists(indexDbPath))
                {
                    throw new InvalidOperationException("Index database file not found: " + indexDbPath);
                }

                // Flush/checkpoint the WAL to ensure data is written to main database
                await index.FlushAsync(token).ConfigureAwait(false);
                _Logging?.Debug(_Header + "WAL checkpoint completed for index '" + indexId + "'");
            }

            // Get index statistics
            IndexStatistics stats = await index.GetStatisticsAsync(token).ConfigureAwait(false);

            // Calculate checksum
            string checksum = await ComputeFileChecksumAsync(indexDbPath, token).ConfigureAwait(false);
            _Logging?.Debug(_Header + "computed checksum for index '" + indexId + "': " + checksum.Substring(0, 16) + "...");

            // Get file size
            FileInfo fileInfo = new FileInfo(indexDbPath);

            // Create manifest
            BackupManifest manifest = new BackupManifest
            {
                Version = SupportedManifestVersion,
                BackupTimestamp = DateTime.UtcNow,
                VerbexVersion = CurrentVerbexVersion,
                SchemaVersion = metadata.SchemaVersion,
                IndexId = indexId,
                TenantId = tenantId,
                Checksum = new BackupChecksum(checksum),
                Statistics = new BackupStatistics(stats.DocumentCount, stats.TermCount, fileInfo.Length)
            };

            // Create backup metadata
            BackupMetadata backupMetadata = new BackupMetadata
            {
                Identifier = metadata.Identifier,
                Name = metadata.Name,
                Description = metadata.Description,
                Enabled = metadata.Enabled,
                InMemory = metadata.InMemory,
                Configuration = new BackupConfiguration
                {
                    MinTokenLength = metadata.MinTokenLength,
                    MaxTokenLength = metadata.MaxTokenLength,
                    EnableLemmatizer = metadata.EnableLemmatizer,
                    EnableStopWordRemover = metadata.EnableStopWordRemover,
                    CacheConfiguration = metadata.CacheConfiguration
                },
                Labels = metadata.Labels,
                Tags = metadata.Tags,
                CustomMetadata = metadata.CustomMetadata
            };

            // Create ZIP archive in memory
            MemoryStream archiveStream = new MemoryStream();
            using (ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                // Add manifest.json
                ZipArchiveEntry manifestEntry = archive.CreateEntry(ManifestFileName, CompressionLevel.Optimal);
                using (Stream entryStream = manifestEntry.Open())
                {
                    string manifestJson = JsonSerializer.Serialize(manifest, _JsonOptions);
                    byte[] manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
                    await entryStream.WriteAsync(manifestBytes, 0, manifestBytes.Length, token).ConfigureAwait(false);
                }

                // Add metadata.json
                ZipArchiveEntry metadataEntry = archive.CreateEntry(MetadataFileName, CompressionLevel.Optimal);
                using (Stream entryStream = metadataEntry.Open())
                {
                    string metadataJson = JsonSerializer.Serialize(backupMetadata, _JsonOptions);
                    byte[] metadataBytes = Encoding.UTF8.GetBytes(metadataJson);
                    await entryStream.WriteAsync(metadataBytes, 0, metadataBytes.Length, token).ConfigureAwait(false);
                }

                // Add index.db
                ZipArchiveEntry dbEntry = archive.CreateEntry(IndexDbFileName, CompressionLevel.Optimal);
                using (Stream entryStream = dbEntry.Open())
                using (FileStream dbFileStream = new FileStream(indexDbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    await dbFileStream.CopyToAsync(entryStream, 81920, token).ConfigureAwait(false);
                }
            }

            // Clean up temp file if we created one for in-memory backup
            if (tempFilePath != null && File.Exists(tempFilePath))
            {
                try
                {
                    File.Delete(tempFilePath);
                    _Logging?.Debug(_Header + "deleted temp file: " + tempFilePath);
                }
                catch (Exception ex)
                {
                    _Logging?.Warn(_Header + "failed to delete temp file: " + ex.Message);
                }
            }

            archiveStream.Position = 0;
            _Logging?.Info(_Header + "backup created for index '" + indexId + "', size: " + archiveStream.Length + " bytes");

            return archiveStream;
        }

        /// <inheritdoc />
        public async Task<RestoreResult> RestoreNewAsync(string tenantId, Stream backupStream, string? newName, string? newId, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (backupStream == null) throw new ArgumentNullException(nameof(backupStream));

            _Logging?.Info(_Header + "starting restore (new) for tenant '" + tenantId + "'");

            // Extract and validate backup
            BackupValidationResult validation = await ValidateAndExtractBackupAsync(backupStream, token).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return RestoreResult.Failed(validation.ErrorMessage);
            }

            BackupManifest manifest = validation.Manifest!;
            BackupMetadata backupMetadata = validation.Metadata!;

            // Check for duplicate index ID if specified
            if (!String.IsNullOrEmpty(newId) && _IndexManager.IndexExists(newId))
            {
                return RestoreResult.Failed("Index with ID '" + newId + "' already exists");
            }

            // Determine new index name
            string indexName = !String.IsNullOrEmpty(newName) ? newName : backupMetadata.Name;

            // Check for duplicate name
            if (_IndexManager.IndexExistsByName(tenantId, indexName))
            {
                // Generate a unique name
                indexName = indexName + "_restored_" + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            }

            // Create new index metadata
            // Note: Restored indices are always created as on-disk for data persistence
            bool originalWasInMemory = backupMetadata.InMemory;
            IndexMetadata newMetadata = new IndexMetadata(tenantId, indexName, backupMetadata.Description)
            {
                Enabled = backupMetadata.Enabled,
                InMemory = false, // Restored indices are always on-disk for data safety
                MinTokenLength = backupMetadata.Configuration?.MinTokenLength ?? 0,
                MaxTokenLength = backupMetadata.Configuration?.MaxTokenLength ?? 0,
                EnableLemmatizer = backupMetadata.Configuration?.EnableLemmatizer ?? false,
                EnableStopWordRemover = backupMetadata.Configuration?.EnableStopWordRemover ?? false,
                Labels = backupMetadata.Labels,
                Tags = backupMetadata.Tags,
                CustomMetadata = backupMetadata.CustomMetadata,
                CacheConfiguration = backupMetadata.Configuration?.CacheConfiguration
            };

            // Override ID if specified
            if (!String.IsNullOrEmpty(newId))
            {
                newMetadata.Identifier = newId;
            }

            // Create index directory
            string indexDirectory = Path.Combine(_DataDirectory, tenantId, newMetadata.Identifier);
            if (!Directory.Exists(indexDirectory))
            {
                Directory.CreateDirectory(indexDirectory);
            }

            // Copy index.db to new location
            string indexDbPath = Path.Combine(indexDirectory, IndexDbFileName);
            await File.WriteAllBytesAsync(indexDbPath, validation.IndexDbData!, token).ConfigureAwait(false);

            // Verify checksum after copy
            string newChecksum = await ComputeFileChecksumAsync(indexDbPath, token).ConfigureAwait(false);
            if (manifest.Checksum != null && !String.Equals(newChecksum, manifest.Checksum.IndexDb, StringComparison.OrdinalIgnoreCase))
            {
                // Clean up
                try { Directory.Delete(indexDirectory, true); } catch { }
                return RestoreResult.Failed("Checksum verification failed after copy");
            }

            // Update the index name in the restored database to match the new identifier
            // This is necessary because InvertedIndex.OpenAsync looks up the index by name
            try
            {
                await UpdateRestoredIndexNameAsync(indexDbPath, newMetadata.Identifier, token).ConfigureAwait(false);
                _Logging?.Debug(_Header + "updated index name in restored database to '" + newMetadata.Identifier + "'");
            }
            catch (Exception ex)
            {
                // Clean up on failure
                try { Directory.Delete(indexDirectory, true); } catch { }
                _Logging?.Error(_Header + "failed to update index name in restored database: " + ex.Message);
                return RestoreResult.Failed("Failed to update index name in restored database: " + ex.Message);
            }

            // Register with IndexManager
            try
            {
                IndexMetadata created = await _IndexManager.CreateIndexAsync(newMetadata, token).ConfigureAwait(false);
                _Logging?.Info(_Header + "restore completed, new index '" + created.Identifier + "' created");

                RestoreResult result = RestoreResult.Successful(created.Identifier, "Index restored successfully as '" + indexName + "'");

                if (originalWasInMemory)
                {
                    result.AddWarning("Original index was in-memory; restored as on-disk for data persistence");
                }

                if (manifest.Statistics != null)
                {
                    result.AddWarning("Original index had " + manifest.Statistics.DocumentCount + " documents and " + manifest.Statistics.TermCount + " terms");
                }

                return result;
            }
            catch (Exception ex)
            {
                // Clean up on failure
                try { Directory.Delete(indexDirectory, true); } catch { }
                _Logging?.Error(_Header + "restore failed: " + ex.Message);
                return RestoreResult.Failed("Failed to register restored index: " + ex.Message);
            }
        }

        /// <inheritdoc />
        public async Task<RestoreResult> RestoreReplaceAsync(string tenantId, string indexId, Stream backupStream, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(tenantId)) throw new ArgumentNullException(nameof(tenantId));
            if (String.IsNullOrEmpty(indexId)) throw new ArgumentNullException(nameof(indexId));
            if (backupStream == null) throw new ArgumentNullException(nameof(backupStream));

            InvertedIndex? index = _IndexManager.GetIndex(indexId);
            IndexMetadata? metadata = _IndexManager.GetMetadata(indexId);

            if (index == null || metadata == null)
            {
                return RestoreResult.Failed("Index not found: " + indexId);
            }

            if (metadata.InMemory)
            {
                return RestoreResult.Failed("Cannot restore to in-memory index. Index '" + indexId + "' is in-memory only.");
            }

            _Logging?.Info(_Header + "starting restore (replace) for index '" + indexId + "'");

            // Extract and validate backup
            BackupValidationResult validation = await ValidateAndExtractBackupAsync(backupStream, token).ConfigureAwait(false);
            if (!validation.IsValid)
            {
                return RestoreResult.Failed(validation.ErrorMessage);
            }

            BackupManifest manifest = validation.Manifest!;

            string indexDirectory = Path.Combine(_DataDirectory, tenantId, indexId);
            string indexDbPath = Path.Combine(indexDirectory, IndexDbFileName);
            string backupDbPath = Path.Combine(indexDirectory, IndexDbFileName + ".backup");

            // Flush current index
            await index.FlushAsync(token).ConfigureAwait(false);

            // Close the index
            await index.CloseAsync(token).ConfigureAwait(false);
            _Logging?.Debug(_Header + "closed index '" + indexId + "' for restore");

            try
            {
                // Create backup of existing database
                if (File.Exists(indexDbPath))
                {
                    File.Copy(indexDbPath, backupDbPath, true);
                }

                // Write new database
                await File.WriteAllBytesAsync(indexDbPath, validation.IndexDbData!, token).ConfigureAwait(false);

                // Verify checksum
                string newChecksum = await ComputeFileChecksumAsync(indexDbPath, token).ConfigureAwait(false);
                if (manifest.Checksum != null && !String.Equals(newChecksum, manifest.Checksum.IndexDb, StringComparison.OrdinalIgnoreCase))
                {
                    // Restore backup
                    if (File.Exists(backupDbPath))
                    {
                        File.Copy(backupDbPath, indexDbPath, true);
                    }
                    await index.OpenAsync(token).ConfigureAwait(false);
                    return RestoreResult.Failed("Checksum verification failed, original database restored");
                }

                // Delete backup copy
                if (File.Exists(backupDbPath))
                {
                    File.Delete(backupDbPath);
                }

                // Update the index name in the restored database to match the target identifier
                await UpdateRestoredIndexNameAsync(indexDbPath, indexId, token).ConfigureAwait(false);
                _Logging?.Debug(_Header + "updated index name in restored database to '" + indexId + "'");

                // Reopen the index
                await index.OpenAsync(token).ConfigureAwait(false);
                _Logging?.Info(_Header + "restore (replace) completed for index '" + indexId + "'");

                RestoreResult result = RestoreResult.Successful(indexId, "Index '" + indexId + "' replaced successfully from backup");

                if (manifest.Statistics != null)
                {
                    result.AddWarning("Restored backup had " + manifest.Statistics.DocumentCount + " documents and " + manifest.Statistics.TermCount + " terms");
                }

                return result;
            }
            catch (Exception ex)
            {
                _Logging?.Error(_Header + "restore (replace) failed: " + ex.Message);

                // Try to restore from backup and reopen
                try
                {
                    if (File.Exists(backupDbPath))
                    {
                        File.Copy(backupDbPath, indexDbPath, true);
                        File.Delete(backupDbPath);
                    }
                    await index.OpenAsync(token).ConfigureAwait(false);
                }
                catch (Exception reopenEx)
                {
                    _Logging?.Error(_Header + "failed to restore original database: " + reopenEx.Message);
                }

                return RestoreResult.Failed("Restore failed: " + ex.Message);
            }
        }

        #endregion

        #region Private-Methods

        private async Task<string> ComputeFileChecksumAsync(string filePath, CancellationToken token)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] hash = await sha256.ComputeHashAsync(stream, token).ConfigureAwait(false);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private async Task<BackupValidationResult> ValidateAndExtractBackupAsync(Stream backupStream, CancellationToken token)
        {
            BackupValidationResult result = new BackupValidationResult();

            try
            {
                // Copy to seekable stream if needed
                MemoryStream memoryStream;
                if (!backupStream.CanSeek)
                {
                    memoryStream = new MemoryStream();
                    await backupStream.CopyToAsync(memoryStream, 81920, token).ConfigureAwait(false);
                    memoryStream.Position = 0;
                }
                else
                {
                    memoryStream = new MemoryStream();
                    await backupStream.CopyToAsync(memoryStream, 81920, token).ConfigureAwait(false);
                    memoryStream.Position = 0;
                }

                using (ZipArchive archive = new ZipArchive(memoryStream, ZipArchiveMode.Read, leaveOpen: false))
                {
                    // Validate required files exist
                    ZipArchiveEntry? manifestEntry = archive.GetEntry(ManifestFileName);
                    ZipArchiveEntry? metadataEntry = archive.GetEntry(MetadataFileName);
                    ZipArchiveEntry? dbEntry = archive.GetEntry(IndexDbFileName);

                    if (manifestEntry == null)
                    {
                        result.ErrorMessage = "Backup archive is missing manifest.json";
                        return result;
                    }

                    if (dbEntry == null)
                    {
                        result.ErrorMessage = "Backup archive is missing index.db";
                        return result;
                    }

                    // Parse manifest
                    using (Stream manifestStream = manifestEntry.Open())
                    using (StreamReader reader = new StreamReader(manifestStream))
                    {
                        string manifestJson = await reader.ReadToEndAsync(token).ConfigureAwait(false);
                        result.Manifest = JsonSerializer.Deserialize<BackupManifest>(manifestJson, _JsonOptions);
                    }

                    if (result.Manifest == null)
                    {
                        result.ErrorMessage = "Failed to parse manifest.json";
                        return result;
                    }

                    // Validate manifest version
                    if (!String.Equals(result.Manifest.Version, SupportedManifestVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        result.ErrorMessage = "Unsupported backup format version: " + result.Manifest.Version + " (expected " + SupportedManifestVersion + ")";
                        return result;
                    }

                    // Parse metadata if present
                    if (metadataEntry != null)
                    {
                        using (Stream metadataStream = metadataEntry.Open())
                        using (StreamReader reader = new StreamReader(metadataStream))
                        {
                            string metadataJson = await reader.ReadToEndAsync(token).ConfigureAwait(false);
                            result.Metadata = JsonSerializer.Deserialize<BackupMetadata>(metadataJson, _JsonOptions);
                        }
                    }

                    // Extract index.db
                    using (Stream dbStream = dbEntry.Open())
                    using (MemoryStream dbMemory = new MemoryStream())
                    {
                        await dbStream.CopyToAsync(dbMemory, 81920, token).ConfigureAwait(false);
                        result.IndexDbData = dbMemory.ToArray();
                    }

                    // Verify checksum if present
                    if (result.Manifest.Checksum != null && !String.IsNullOrEmpty(result.Manifest.Checksum.IndexDb))
                    {
                        using (SHA256 sha256 = SHA256.Create())
                        {
                            byte[] hash = sha256.ComputeHash(result.IndexDbData);
                            string computedChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                            if (!String.Equals(computedChecksum, result.Manifest.Checksum.IndexDb, StringComparison.OrdinalIgnoreCase))
                            {
                                result.ErrorMessage = "Checksum verification failed. Backup may be corrupted.";
                                return result;
                            }
                        }
                    }

                    result.IsValid = true;
                }
            }
            catch (InvalidDataException)
            {
                result.ErrorMessage = "Invalid backup archive format. File is not a valid ZIP archive.";
            }
            catch (JsonException ex)
            {
                result.ErrorMessage = "Failed to parse backup metadata: " + ex.Message;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = "Error processing backup: " + ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Updates the index name in a restored database to match the new identifier.
        /// This is necessary because InvertedIndex.OpenAsync looks up the index by name,
        /// and the restored database contains the original index name/identifier.
        /// </summary>
        /// <param name="dbPath">Path to the index.db file.</param>
        /// <param name="newIndexName">The new index name/identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        private async Task UpdateRestoredIndexNameAsync(string dbPath, string newIndexName, CancellationToken token = default)
        {
            string connectionString = $"Data Source={dbPath}";

            using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                // Update the index name in the indexes table
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE indexes SET name = @newName";
                    command.Parameters.AddWithValue("@newName", newIndexName);

                    int rowsAffected = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    _Logging?.Debug(_Header + "UpdateRestoredIndexNameAsync: updated " + rowsAffected + " rows in indexes table");
                }
            }
        }

        #endregion

        #region Private-Classes

        private class BackupValidationResult
        {
            public bool IsValid { get; set; } = false;
            public string ErrorMessage { get; set; } = string.Empty;
            public BackupManifest? Manifest { get; set; }
            public BackupMetadata? Metadata { get; set; }
            public byte[]? IndexDbData { get; set; }
        }

        #endregion
    }
}
