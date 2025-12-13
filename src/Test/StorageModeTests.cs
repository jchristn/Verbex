namespace Test
{
    using System;
    using System.IO;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for storage mode functionality.
    /// </summary>
    public static class StorageModeTests
    {
        /// <summary>
        /// Runs all storage mode tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("InMemory Mode Test", TestInMemoryModeAsync).ConfigureAwait(false);
            await runner.RunTestAsync("OnDisk Mode Test", TestOnDiskModeAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Mode Persistence Test", TestModePersistenceAsync).ConfigureAwait(false);
        }

        private static async Task TestInMemoryModeAsync()
        {
            VerbexConfiguration config = VerbexConfiguration.CreateInMemory();

            await using InvertedIndex index = new InvertedIndex("test_memory", config);
            await index.OpenAsync().ConfigureAwait(false);

            TestAssert.AreEqual(StorageMode.InMemory, config.StorageMode);

            await index.AddDocumentAsync("doc1.txt", "test content").ConfigureAwait(false);

            long count = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(1L, count);

            SearchResults results = await index.SearchAsync("test").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            IndexStatistics stats = await index.GetStatisticsAsync().ConfigureAwait(false);
            TestAssert.AreEqual(1L, stats.DocumentCount);
        }

        private static async Task TestOnDiskModeAsync()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "VerbexTest", Guid.NewGuid().ToString("N"));

            try
            {
                VerbexConfiguration config = VerbexConfiguration.CreateOnDisk(tempDir, "test.db");

                await using InvertedIndex index = new InvertedIndex("test_disk", config);
                await index.OpenAsync().ConfigureAwait(false);

                TestAssert.AreEqual(StorageMode.OnDisk, config.StorageMode);

                await index.AddDocumentAsync("doc1.txt", "test content").ConfigureAwait(false);

                long count = await index.GetDocumentCountAsync().ConfigureAwait(false);
                TestAssert.AreEqual(1L, count);

                SearchResults results = await index.SearchAsync("test").ConfigureAwait(false);
                TestAssert.AreEqual(1, results.TotalCount);

                IndexStatistics stats = await index.GetStatisticsAsync().ConfigureAwait(false);
                TestAssert.AreEqual(1L, stats.DocumentCount);
            }
            finally
            {
                TestContext.CleanupTestDirectory(tempDir);
            }
        }

        private static async Task TestModePersistenceAsync()
        {
            string tempDir = Path.Combine(Path.GetTempPath(), "VerbexTest", Guid.NewGuid().ToString("N"));

            try
            {
                string docId;

                // Create and populate index
                {
                    VerbexConfiguration config = VerbexConfiguration.CreateOnDisk(tempDir, "persist.db");

                    await using InvertedIndex index = new InvertedIndex("test_persist", config);
                    await index.OpenAsync().ConfigureAwait(false);

                    docId = await index.AddDocumentAsync("doc1.txt", "persistent content").ConfigureAwait(false);
                    await index.FlushAsync().ConfigureAwait(false);
                }

                // Reopen and verify data persisted
                {
                    VerbexConfiguration config = VerbexConfiguration.CreateOnDisk(tempDir, "persist.db");

                    await using InvertedIndex index = new InvertedIndex("test_persist", config);
                    await index.OpenAsync().ConfigureAwait(false);

                    long count = await index.GetDocumentCountAsync().ConfigureAwait(false);
                    TestAssert.AreEqual(1L, count);

                    bool exists = await index.DocumentExistsAsync(docId).ConfigureAwait(false);
                    TestAssert.IsTrue(exists);

                    SearchResults results = await index.SearchAsync("persistent").ConfigureAwait(false);
                    TestAssert.AreEqual(1, results.TotalCount);
                }
            }
            finally
            {
                TestContext.CleanupTestDirectory(tempDir);
            }
        }
    }
}
