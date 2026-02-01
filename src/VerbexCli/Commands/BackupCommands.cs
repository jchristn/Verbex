namespace VerbexCli.Commands
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Invocation;
    using System.IO;
    using System.IO.Compression;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex;
    using VerbexCli.Infrastructure;

    /// <summary>
    /// Commands for backup and restore operations
    /// </summary>
    public static class BackupCommands
    {
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

        /// <summary>
        /// Creates the backup command
        /// </summary>
        /// <returns>Backup command</returns>
        public static Command CreateBackupCommand()
        {
            Command backupCommand = new Command("backup", "Create a backup of an index");

            Argument<string> nameArgument = new Argument<string>("name", "Name of the index to backup");

            Option<string?> outputOption = new Option<string?>(
                aliases: new[] { "--output", "-o" },
                description: "Output file path (default: <index-name>.vbx)")
            {
                IsRequired = false
            };

            Option<string?> outputDirOption = new Option<string?>(
                aliases: new[] { "--output-dir", "-d" },
                description: "Output directory (backup will be named <index-name>_<timestamp>.vbx)")
            {
                IsRequired = false
            };

            backupCommand.AddArgument(nameArgument);
            backupCommand.AddOption(outputOption);
            backupCommand.AddOption(outputDirOption);

            backupCommand.SetHandler(async (InvocationContext context) =>
            {
                string name = context.ParseResult.GetValueForArgument(nameArgument);
                string? output = context.ParseResult.GetValueForOption(outputOption);
                string? outputDir = context.ParseResult.GetValueForOption(outputDirOption);

                await HandleBackupAsync(name, output, outputDir).ConfigureAwait(false);
            });

            return backupCommand;
        }

        /// <summary>
        /// Creates the restore command
        /// </summary>
        /// <returns>Restore command</returns>
        public static Command CreateRestoreCommand()
        {
            Command restoreCommand = new Command("restore", "Restore an index from a backup file");

            Argument<string> fileArgument = new Argument<string>("file", "Backup file path (.vbx)");

            Option<string?> nameOption = new Option<string?>(
                aliases: new[] { "--name", "-n" },
                description: "Name for the restored index (default: original name)")
            {
                IsRequired = false
            };

            Option<string?> replaceOption = new Option<string?>(
                aliases: new[] { "--replace", "-r" },
                description: "Replace an existing index with the backup contents")
            {
                IsRequired = false
            };

            Option<bool> forceOption = new Option<bool>(
                aliases: new[] { "--force", "-f" },
                description: "Force restore without confirmation (for replace)")
            {
                IsRequired = false
            };

            restoreCommand.AddArgument(fileArgument);
            restoreCommand.AddOption(nameOption);
            restoreCommand.AddOption(replaceOption);
            restoreCommand.AddOption(forceOption);

            restoreCommand.SetHandler(async (InvocationContext context) =>
            {
                string file = context.ParseResult.GetValueForArgument(fileArgument);
                string? name = context.ParseResult.GetValueForOption(nameOption);
                string? replace = context.ParseResult.GetValueForOption(replaceOption);
                bool force = context.ParseResult.GetValueForOption(forceOption);

                await HandleRestoreAsync(file, name, replace, force).ConfigureAwait(false);
            });

            return restoreCommand;
        }

        /// <summary>
        /// Handles the backup command
        /// </summary>
        private static async Task HandleBackupAsync(string name, string? output, string? outputDir)
        {
            try
            {
                OutputManager.WriteVerbose($"Creating backup of index '{name}'");

                // Get the index
                if (!IndexManager.Instance.Configurations.ContainsKey(name))
                {
                    OutputManager.WriteError($"Index '{name}' not found");
                    return;
                }

                IndexConfiguration config = IndexManager.Instance.Configurations[name];

                // Check if index is on-disk
                if (config.VerbexConfig.StorageMode != StorageMode.OnDisk)
                {
                    OutputManager.WriteError($"Cannot backup in-memory index '{name}'. Only on-disk indices can be backed up.");
                    return;
                }

                string? storageDirectory = config.VerbexConfig.StorageDirectory;
                if (string.IsNullOrEmpty(storageDirectory))
                {
                    OutputManager.WriteError($"Index '{name}' has no storage directory configured");
                    return;
                }

                string indexDbPath = Path.Combine(storageDirectory, IndexDbFileName);
                if (!File.Exists(indexDbPath))
                {
                    OutputManager.WriteError($"Index database file not found: {indexDbPath}");
                    return;
                }

                // Ensure the index is loaded and flushed
                await IndexManager.Instance.FlushAsync(name).ConfigureAwait(false);
                OutputManager.WriteVerbose("WAL checkpoint completed");

                // Determine output path
                string outputPath;
                if (!string.IsNullOrEmpty(output))
                {
                    outputPath = Path.GetFullPath(output);
                }
                else if (!string.IsNullOrEmpty(outputDir))
                {
                    string timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
                    outputPath = Path.Combine(Path.GetFullPath(outputDir), $"{name}_{timestamp}.vbx");
                }
                else
                {
                    outputPath = Path.GetFullPath($"{name}.vbx");
                }

                // Ensure output directory exists
                string? outputDirectory = Path.GetDirectoryName(outputPath);
                if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // Get index statistics
                object statsObj = await IndexManager.Instance.GetStatisticsAsync(name).ConfigureAwait(false);
                dynamic stats = statsObj;
                long documentCount = stats.Documents;
                long termCount = stats.Terms;

                // Calculate checksum
                string checksum = await ComputeFileChecksumAsync(indexDbPath).ConfigureAwait(false);
                OutputManager.WriteVerbose($"Computed checksum: {checksum.Substring(0, 16)}...");

                // Get file size
                FileInfo fileInfo = new FileInfo(indexDbPath);

                // Create manifest
                BackupManifestDto manifest = new BackupManifestDto
                {
                    Version = SupportedManifestVersion,
                    BackupTimestamp = DateTime.UtcNow,
                    VerbexVersion = CurrentVerbexVersion,
                    SchemaVersion = "2",
                    IndexId = name,
                    TenantId = "cli",
                    Checksum = new BackupChecksumDto { Algorithm = "SHA256", IndexDb = checksum },
                    Statistics = new BackupStatisticsDto
                    {
                        DocumentCount = documentCount,
                        TermCount = termCount,
                        TotalSize = fileInfo.Length
                    }
                };

                // Create metadata
                BackupMetadataDto metadata = new BackupMetadataDto
                {
                    Identifier = name,
                    Name = name,
                    Description = config.Description,
                    Enabled = true,
                    InMemory = false,
                    Configuration = new BackupConfigurationDto
                    {
                        MinTokenLength = config.VerbexConfig.MinTokenLength,
                        MaxTokenLength = config.VerbexConfig.MaxTokenLength,
                        EnableLemmatizer = config.VerbexConfig.Lemmatizer != null,
                        EnableStopWordRemover = config.VerbexConfig.StopWordRemover != null
                    }
                };

                // Create ZIP archive
                OutputManager.WriteInfo($"Creating backup archive...");

                using (FileStream archiveStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write))
                using (ZipArchive archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: false))
                {
                    // Add manifest.json
                    ZipArchiveEntry manifestEntry = archive.CreateEntry(ManifestFileName, CompressionLevel.Optimal);
                    using (Stream entryStream = manifestEntry.Open())
                    {
                        string manifestJson = JsonSerializer.Serialize(manifest, _JsonOptions);
                        byte[] manifestBytes = Encoding.UTF8.GetBytes(manifestJson);
                        await entryStream.WriteAsync(manifestBytes, 0, manifestBytes.Length).ConfigureAwait(false);
                    }

                    // Add metadata.json
                    ZipArchiveEntry metadataEntry = archive.CreateEntry(MetadataFileName, CompressionLevel.Optimal);
                    using (Stream entryStream = metadataEntry.Open())
                    {
                        string metadataJson = JsonSerializer.Serialize(metadata, _JsonOptions);
                        byte[] metadataBytes = Encoding.UTF8.GetBytes(metadataJson);
                        await entryStream.WriteAsync(metadataBytes, 0, metadataBytes.Length).ConfigureAwait(false);
                    }

                    // Add index.db
                    ZipArchiveEntry dbEntry = archive.CreateEntry(IndexDbFileName, CompressionLevel.Optimal);
                    using (Stream entryStream = dbEntry.Open())
                    using (FileStream dbFileStream = new FileStream(indexDbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        await dbFileStream.CopyToAsync(entryStream, 81920).ConfigureAwait(false);
                    }
                }

                FileInfo outputFileInfo = new FileInfo(outputPath);
                OutputManager.WriteSuccess($"Backup created: {outputPath}");
                OutputManager.WriteInfo($"Size: {FormatBytes(outputFileInfo.Length)} (original: {FormatBytes(fileInfo.Length)})");
                OutputManager.WriteInfo($"Documents: {documentCount}, Terms: {termCount}");
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Backup failed: {ex.Message}");
                if (Environment.GetEnvironmentVariable("VBX_DEBUG") == "1")
                {
                    OutputManager.WriteError(ex.StackTrace ?? "No stack trace available");
                }
            }
        }

        /// <summary>
        /// Handles the restore command
        /// </summary>
        private static async Task HandleRestoreAsync(string file, string? name, string? replace, bool force)
        {
            try
            {
                string filePath = Path.GetFullPath(file);

                if (!File.Exists(filePath))
                {
                    OutputManager.WriteError($"Backup file not found: {filePath}");
                    return;
                }

                OutputManager.WriteVerbose($"Reading backup file: {filePath}");

                // Validate and extract backup
                BackupValidationResult validation = await ValidateAndExtractBackupAsync(filePath).ConfigureAwait(false);
                if (!validation.IsValid)
                {
                    OutputManager.WriteError($"Invalid backup: {validation.ErrorMessage}");
                    return;
                }

                BackupManifestDto manifest = validation.Manifest!;
                BackupMetadataDto metadata = validation.Metadata!;

                OutputManager.WriteInfo($"Backup info:");
                OutputManager.WriteInfo($"  Original index: {metadata.Name}");
                OutputManager.WriteInfo($"  Created: {manifest.BackupTimestamp:yyyy-MM-dd HH:mm:ss} UTC");
                OutputManager.WriteInfo($"  Verbex version: {manifest.VerbexVersion}");
                if (manifest.Statistics != null)
                {
                    OutputManager.WriteInfo($"  Documents: {manifest.Statistics.DocumentCount}, Terms: {manifest.Statistics.TermCount}");
                }

                if (!string.IsNullOrEmpty(replace))
                {
                    // Replace existing index
                    await HandleRestoreReplaceAsync(replace, validation, force).ConfigureAwait(false);
                }
                else
                {
                    // Create new index
                    await HandleRestoreNewAsync(name, validation).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Restore failed: {ex.Message}");
                if (Environment.GetEnvironmentVariable("VBX_DEBUG") == "1")
                {
                    OutputManager.WriteError(ex.StackTrace ?? "No stack trace available");
                }
            }
        }

        private static async Task HandleRestoreNewAsync(string? name, BackupValidationResult validation)
        {
            BackupMetadataDto metadata = validation.Metadata!;
            BackupManifestDto manifest = validation.Manifest!;

            // Determine index name
            string indexName = name ?? metadata.Name;

            // Check if name already exists
            if (IndexManager.Instance.Configurations.ContainsKey(indexName))
            {
                string timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                indexName = $"{indexName}_restored_{timestamp}";
                OutputManager.WriteInfo($"Index name already exists, using: {indexName}");
            }

            OutputManager.WriteInfo($"Restoring as new index: {indexName}");

            // Create new index
            bool enableLemmatizer = metadata.Configuration?.EnableLemmatizer ?? false;
            bool enableStopWords = metadata.Configuration?.EnableStopWordRemover ?? false;
            int minTokenLength = metadata.Configuration?.MinTokenLength ?? 0;
            int maxTokenLength = metadata.Configuration?.MaxTokenLength ?? 0;

            await IndexManager.Instance.CreateIndexAsync(
                indexName,
                "disk",
                enableLemmatizer,
                enableStopWords,
                minTokenLength,
                maxTokenLength).ConfigureAwait(false);

            // Get the storage directory
            IndexConfiguration config = IndexManager.Instance.Configurations[indexName];
            string? storageDirectory = config.VerbexConfig.StorageDirectory;

            if (string.IsNullOrEmpty(storageDirectory))
            {
                OutputManager.WriteError("Failed to get storage directory for new index");
                await IndexManager.Instance.DeleteIndexAsync(indexName).ConfigureAwait(false);
                return;
            }

            // Close and dispose the index so we can replace the database
            await IndexManager.Instance.FlushAsync(indexName).ConfigureAwait(false);

            // Delete the newly created empty database and replace with backup
            string indexDbPath = Path.Combine(storageDirectory, IndexDbFileName);

            // Close the index before replacing the file
            // Note: We need to ensure the index is closed. The IndexManager doesn't expose CloseAsync directly,
            // so we'll delete and recreate the configuration entry.

            // Delete the index (this closes and removes it)
            await IndexManager.Instance.DeleteIndexAsync(indexName).ConfigureAwait(false);

            // Create the directory if needed
            if (!Directory.Exists(storageDirectory))
            {
                Directory.CreateDirectory(storageDirectory);
            }

            // Write the backup database
            await File.WriteAllBytesAsync(indexDbPath, validation.IndexDbData!).ConfigureAwait(false);

            // Verify checksum
            if (manifest.Checksum != null && !string.IsNullOrEmpty(manifest.Checksum.IndexDb))
            {
                string newChecksum = await ComputeFileChecksumAsync(indexDbPath).ConfigureAwait(false);
                if (!string.Equals(newChecksum, manifest.Checksum.IndexDb, StringComparison.OrdinalIgnoreCase))
                {
                    OutputManager.WriteError("Checksum verification failed. Backup may be corrupted.");
                    try { Directory.Delete(storageDirectory, true); } catch { }
                    return;
                }
                OutputManager.WriteVerbose("Checksum verified");
            }

            // Recreate the index entry pointing to the restored database
            await IndexManager.Instance.CreateIndexAsync(
                indexName,
                "disk",
                enableLemmatizer,
                enableStopWords,
                minTokenLength,
                maxTokenLength).ConfigureAwait(false);

            OutputManager.WriteSuccess($"Index '{indexName}' restored successfully");

            if (manifest.Statistics != null)
            {
                OutputManager.WriteInfo($"Restored {manifest.Statistics.DocumentCount} documents and {manifest.Statistics.TermCount} terms");
            }
        }

        private static async Task HandleRestoreReplaceAsync(string indexName, BackupValidationResult validation, bool force)
        {
            BackupManifestDto manifest = validation.Manifest!;

            // Check if index exists
            if (!IndexManager.Instance.Configurations.ContainsKey(indexName))
            {
                OutputManager.WriteError($"Index '{indexName}' not found");
                return;
            }

            IndexConfiguration config = IndexManager.Instance.Configurations[indexName];

            // Check if index is on-disk
            if (config.VerbexConfig.StorageMode != StorageMode.OnDisk)
            {
                OutputManager.WriteError($"Cannot replace in-memory index '{indexName}'. Only on-disk indices can be replaced.");
                return;
            }

            // Confirm if not forced
            if (!force)
            {
                OutputManager.WriteLine($"Are you sure you want to replace index '{indexName}'? This will overwrite all existing data. (y/N)");
                string? response = Console.ReadLine();
                if (response?.ToLowerInvariant() != "y" && response?.ToLowerInvariant() != "yes")
                {
                    OutputManager.WriteLine("Operation cancelled");
                    return;
                }
            }

            string? storageDirectory = config.VerbexConfig.StorageDirectory;
            if (string.IsNullOrEmpty(storageDirectory))
            {
                OutputManager.WriteError($"Index '{indexName}' has no storage directory configured");
                return;
            }

            string indexDbPath = Path.Combine(storageDirectory, IndexDbFileName);
            string backupDbPath = Path.Combine(storageDirectory, IndexDbFileName + ".backup");

            OutputManager.WriteInfo($"Replacing index '{indexName}'...");

            // Flush current index
            await IndexManager.Instance.FlushAsync(indexName).ConfigureAwait(false);

            // Save configuration for recreation
            bool enableLemmatizer = config.VerbexConfig.Lemmatizer != null;
            bool enableStopWords = config.VerbexConfig.StopWordRemover != null;
            int minTokenLength = config.VerbexConfig.MinTokenLength;
            int maxTokenLength = config.VerbexConfig.MaxTokenLength;

            // Delete the index (this closes it)
            await IndexManager.Instance.DeleteIndexAsync(indexName).ConfigureAwait(false);

            try
            {
                // Create backup of existing database
                if (File.Exists(indexDbPath))
                {
                    File.Copy(indexDbPath, backupDbPath, true);
                }

                // Create directory if needed
                if (!Directory.Exists(storageDirectory))
                {
                    Directory.CreateDirectory(storageDirectory);
                }

                // Write new database
                await File.WriteAllBytesAsync(indexDbPath, validation.IndexDbData!).ConfigureAwait(false);

                // Verify checksum
                if (manifest.Checksum != null && !string.IsNullOrEmpty(manifest.Checksum.IndexDb))
                {
                    string newChecksum = await ComputeFileChecksumAsync(indexDbPath).ConfigureAwait(false);
                    if (!string.Equals(newChecksum, manifest.Checksum.IndexDb, StringComparison.OrdinalIgnoreCase))
                    {
                        // Restore backup
                        if (File.Exists(backupDbPath))
                        {
                            File.Copy(backupDbPath, indexDbPath, true);
                        }
                        OutputManager.WriteError("Checksum verification failed. Original database restored.");
                        return;
                    }
                    OutputManager.WriteVerbose("Checksum verified");
                }

                // Delete backup copy
                if (File.Exists(backupDbPath))
                {
                    File.Delete(backupDbPath);
                }

                // Recreate the index entry
                await IndexManager.Instance.CreateIndexAsync(
                    indexName,
                    "disk",
                    enableLemmatizer,
                    enableStopWords,
                    minTokenLength,
                    maxTokenLength).ConfigureAwait(false);

                OutputManager.WriteSuccess($"Index '{indexName}' replaced successfully");

                if (manifest.Statistics != null)
                {
                    OutputManager.WriteInfo($"Restored {manifest.Statistics.DocumentCount} documents and {manifest.Statistics.TermCount} terms");
                }
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Restore failed: {ex.Message}");

                // Try to restore from backup
                try
                {
                    if (File.Exists(backupDbPath))
                    {
                        File.Copy(backupDbPath, indexDbPath, true);
                        File.Delete(backupDbPath);
                        OutputManager.WriteInfo("Original database restored from backup");
                    }

                    // Recreate the index entry
                    await IndexManager.Instance.CreateIndexAsync(
                        indexName,
                        "disk",
                        enableLemmatizer,
                        enableStopWords,
                        minTokenLength,
                        maxTokenLength).ConfigureAwait(false);
                }
                catch (Exception reopenEx)
                {
                    OutputManager.WriteError($"Failed to restore original database: {reopenEx.Message}");
                }
            }
        }

        private static async Task<string> ComputeFileChecksumAsync(string filePath)
        {
            using (SHA256 sha256 = SHA256.Create())
            using (FileStream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                byte[] hash = await sha256.ComputeHashAsync(stream).ConfigureAwait(false);
                return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            }
        }

        private static async Task<BackupValidationResult> ValidateAndExtractBackupAsync(string filePath)
        {
            BackupValidationResult result = new BackupValidationResult();

            try
            {
                using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Read, leaveOpen: false))
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
                        string manifestJson = await reader.ReadToEndAsync().ConfigureAwait(false);
                        result.Manifest = JsonSerializer.Deserialize<BackupManifestDto>(manifestJson, _JsonOptions);
                    }

                    if (result.Manifest == null)
                    {
                        result.ErrorMessage = "Failed to parse manifest.json";
                        return result;
                    }

                    // Validate manifest version
                    if (!string.Equals(result.Manifest.Version, SupportedManifestVersion, StringComparison.OrdinalIgnoreCase))
                    {
                        result.ErrorMessage = $"Unsupported backup format version: {result.Manifest.Version} (expected {SupportedManifestVersion})";
                        return result;
                    }

                    // Parse metadata if present
                    if (metadataEntry != null)
                    {
                        using (Stream metadataStream = metadataEntry.Open())
                        using (StreamReader reader = new StreamReader(metadataStream))
                        {
                            string metadataJson = await reader.ReadToEndAsync().ConfigureAwait(false);
                            result.Metadata = JsonSerializer.Deserialize<BackupMetadataDto>(metadataJson, _JsonOptions);
                        }
                    }
                    else
                    {
                        // Create default metadata
                        result.Metadata = new BackupMetadataDto
                        {
                            Identifier = result.Manifest.IndexId ?? "restored",
                            Name = result.Manifest.IndexId ?? "restored",
                            Description = "Restored from backup",
                            Enabled = true,
                            InMemory = false
                        };
                    }

                    // Extract index.db
                    using (Stream dbStream = dbEntry.Open())
                    using (MemoryStream dbMemory = new MemoryStream())
                    {
                        await dbStream.CopyToAsync(dbMemory, 81920).ConfigureAwait(false);
                        result.IndexDbData = dbMemory.ToArray();
                    }

                    // Verify checksum if present
                    if (result.Manifest.Checksum != null && !string.IsNullOrEmpty(result.Manifest.Checksum.IndexDb))
                    {
                        using (SHA256 sha256 = SHA256.Create())
                        {
                            byte[] hash = sha256.ComputeHash(result.IndexDbData);
                            string computedChecksum = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();

                            if (!string.Equals(computedChecksum, result.Manifest.Checksum.IndexDb, StringComparison.OrdinalIgnoreCase))
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
                result.ErrorMessage = $"Failed to parse backup metadata: {ex.Message}";
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Error processing backup: {ex.Message}";
            }

            return result;
        }

        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size = size / 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }

        #region Private-DTOs

        private class BackupValidationResult
        {
            public bool IsValid { get; set; } = false;
            public string ErrorMessage { get; set; } = string.Empty;
            public BackupManifestDto? Manifest { get; set; }
            public BackupMetadataDto? Metadata { get; set; }
            public byte[]? IndexDbData { get; set; }
        }

        private class BackupManifestDto
        {
            public string Version { get; set; } = string.Empty;
            public DateTime BackupTimestamp { get; set; }
            public string VerbexVersion { get; set; } = string.Empty;
            public string SchemaVersion { get; set; } = string.Empty;
            public string? IndexId { get; set; }
            public string? TenantId { get; set; }
            public BackupChecksumDto? Checksum { get; set; }
            public BackupStatisticsDto? Statistics { get; set; }
        }

        private class BackupChecksumDto
        {
            public string Algorithm { get; set; } = "SHA256";
            public string IndexDb { get; set; } = string.Empty;
        }

        private class BackupStatisticsDto
        {
            public long DocumentCount { get; set; }
            public long TermCount { get; set; }
            public long TotalSize { get; set; }
        }

        private class BackupMetadataDto
        {
            public string Identifier { get; set; } = string.Empty;
            public string Name { get; set; } = string.Empty;
            public string? Description { get; set; }
            public bool Enabled { get; set; } = true;
            public bool InMemory { get; set; } = false;
            public BackupConfigurationDto? Configuration { get; set; }
        }

        private class BackupConfigurationDto
        {
            public int MinTokenLength { get; set; }
            public int MaxTokenLength { get; set; }
            public bool EnableLemmatizer { get; set; }
            public bool EnableStopWordRemover { get; set; }
        }

        #endregion
    }
}
