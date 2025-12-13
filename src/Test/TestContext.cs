namespace Test
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Test context that manages storage mode settings for all tests.
    /// </summary>
    public static class TestContext
    {
        private static StorageMode _CurrentStorageMode = StorageMode.InMemory;

        /// <summary>
        /// Gets the current storage mode for tests.
        /// </summary>
        public static StorageMode CurrentStorageMode => _CurrentStorageMode;

        /// <summary>
        /// Sets the storage mode for tests.
        /// </summary>
        /// <param name="storageMode">Storage mode to use.</param>
        public static void SetStorageMode(StorageMode storageMode)
        {
            _CurrentStorageMode = storageMode;
        }

        /// <summary>
        /// Clears the current storage mode settings.
        /// </summary>
        public static void ClearStorageMode()
        {
            _CurrentStorageMode = StorageMode.InMemory;
        }

        /// <summary>
        /// Creates an InvertedIndex using the current test context settings and opens it.
        /// </summary>
        /// <param name="indexName">Name for the index.</param>
        /// <param name="customConfig">Optional custom configuration.</param>
        /// <returns>Task returning an opened InvertedIndex configured for current test context.</returns>
        public static async Task<InvertedIndex> CreateTestIndexAsync(string? indexName = null, VerbexConfiguration? customConfig = null)
        {
            string name = indexName ?? $"test_{Guid.NewGuid():N}";
            VerbexConfiguration config = customConfig?.Clone() ?? new VerbexConfiguration();

            config.StorageMode = _CurrentStorageMode;

            if (_CurrentStorageMode == StorageMode.OnDisk)
            {
                config.StorageDirectory = Path.Combine(Path.GetTempPath(), "VerbexTest", name);
            }

            InvertedIndex index = new InvertedIndex(name, config);
            await index.OpenAsync().ConfigureAwait(false);
            return index;
        }

        /// <summary>
        /// Creates a VerbexConfiguration using the current test context settings.
        /// </summary>
        /// <returns>VerbexConfiguration for current test context.</returns>
        public static VerbexConfiguration CreateTestConfiguration()
        {
            VerbexConfiguration config = new VerbexConfiguration
            {
                StorageMode = _CurrentStorageMode
            };

            if (_CurrentStorageMode == StorageMode.OnDisk)
            {
                config.StorageDirectory = Path.Combine(Path.GetTempPath(), "VerbexTest", Guid.NewGuid().ToString("N"));
            }

            return config;
        }

        /// <summary>
        /// Cleans up test storage directory.
        /// </summary>
        /// <param name="directory">Directory to clean.</param>
        public static void CleanupTestDirectory(string? directory)
        {
            if (string.IsNullOrEmpty(directory))
            {
                return;
            }

            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
