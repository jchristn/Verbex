namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for document metadata retrieval functionality.
    /// </summary>
    public static class DocumentMetadataRetrievalTests
    {
        /// <summary>
        /// Runs all document metadata retrieval tests.
        /// </summary>
        /// <param name="runner">Test runner to use.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("GetDocumentAsync returns document by ID", TestGetDocumentByIdAsync).ConfigureAwait(false);
            await runner.RunTestAsync("GetDocumentByNameAsync returns document by name", TestGetDocumentByNameAsync).ConfigureAwait(false);
            await runner.RunTestAsync("GetDocumentByNameAsync returns null for nonexistent name", TestGetDocumentByNonexistentNameAsync).ConfigureAwait(false);
            await runner.RunTestAsync("GetDocumentsAsync returns all documents with pagination", TestGetDocumentsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("DocumentExistsAsync returns true for existing document", TestDocumentExistsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("DocumentExistsByNameAsync returns true for existing document", TestDocumentExistsByNameAsync).ConfigureAwait(false);
            await runner.RunTestAsync("GetDefaultStorageDirectory returns valid path", TestGetDefaultStorageDirectoryAsync).ConfigureAwait(false);
            await runner.RunTestAsync("CreateOnDisk configuration has correct settings", TestCreateOnDiskConfigurationAsync).ConfigureAwait(false);
        }

        private static async Task TestGetDocumentByIdAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("test.txt", "test content for id retrieval").ConfigureAwait(false);

            DocumentMetadata? metadata = await index.GetDocumentAsync(docId).ConfigureAwait(false);

            TestAssert.IsNotNull(metadata);
            TestAssert.AreEqual(docId, metadata!.DocumentId);
        }

        private static async Task TestGetDocumentByNameAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("path/to/myfile.txt", "test content").ConfigureAwait(false);

            DocumentMetadata? metadata = await index.GetDocumentByNameAsync("path/to/myfile.txt").ConfigureAwait(false);

            TestAssert.IsNotNull(metadata);
            TestAssert.AreEqual(docId, metadata!.DocumentId);
            TestAssert.AreEqual("path/to/myfile.txt", metadata.DocumentPath);
        }

        private static async Task TestGetDocumentByNonexistentNameAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("existing.txt", "test content").ConfigureAwait(false);

            DocumentMetadata? metadata = await index.GetDocumentByNameAsync("nonexistent.txt").ConfigureAwait(false);

            TestAssert.IsNull(metadata);
        }

        private static async Task TestGetDocumentsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "content 1").ConfigureAwait(false);
            await index.AddDocumentAsync("doc2.txt", "content 2").ConfigureAwait(false);
            await index.AddDocumentAsync("doc3.txt", "content 3").ConfigureAwait(false);

            List<DocumentMetadata> allDocs = await index.GetDocumentsAsync(100, 0).ConfigureAwait(false);

            TestAssert.AreEqual(3, allDocs.Count);
        }

        private static async Task TestDocumentExistsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("doc.txt", "test content").ConfigureAwait(false);

            bool exists = await index.DocumentExistsAsync(docId).ConfigureAwait(false);
            bool notExists = await index.DocumentExistsAsync("nonexistent-id-12345").ConfigureAwait(false);

            TestAssert.IsTrue(exists);
            TestAssert.IsFalse(notExists);
        }

        private static async Task TestDocumentExistsByNameAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("existing.txt", "test content").ConfigureAwait(false);

            bool exists = await index.DocumentExistsByNameAsync("existing.txt").ConfigureAwait(false);
            bool notExists = await index.DocumentExistsByNameAsync("nonexistent.txt").ConfigureAwait(false);

            TestAssert.IsTrue(exists);
            TestAssert.IsFalse(notExists);
        }

        private static async Task TestGetDefaultStorageDirectoryAsync()
        {
            string indexName = "testindex";
            string defaultDir = VerbexConfiguration.GetDefaultStorageDirectory(indexName);

            string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string expectedPath = Path.Combine(userProfile, ".vbx", "indices", indexName);

            TestAssert.AreEqual(expectedPath, defaultDir);

            // Test null throws
            bool threwException = false;
            try
            {
                VerbexConfiguration.GetDefaultStorageDirectory(null!);
            }
            catch (ArgumentNullException)
            {
                threwException = true;
            }
            TestAssert.IsTrue(threwException);

            // Test empty string throws
            threwException = false;
            try
            {
                VerbexConfiguration.GetDefaultStorageDirectory("   ");
            }
            catch (ArgumentException)
            {
                threwException = true;
            }
            TestAssert.IsTrue(threwException);

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestCreateOnDiskConfigurationAsync()
        {
            string indexName = "testindex";
            VerbexConfiguration config = VerbexConfiguration.CreateOnDisk(indexName);

            string expectedDir = VerbexConfiguration.GetDefaultStorageDirectory(indexName);

            TestAssert.AreEqual(StorageMode.OnDisk, config.StorageMode);
            TestAssert.AreEqual(expectedDir, config.StorageDirectory);

            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
