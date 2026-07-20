namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex;
    using Verbex.DTO.Requests;
    using Verbex.DTO.Responses;

    /// <summary>
    /// Tests for batch document add functionality.
    /// </summary>
    public static class BatchAddDocumentsTests
    {
        /// <summary>
        /// Runs all batch document add tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("Batch Add All Success Test", TestBatchAddAllSuccessAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Add With Custom IDs Test", TestBatchAddWithCustomIdsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Add With Duplicate Custom IDs Test", TestBatchAddWithDuplicateCustomIdsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Add With Metadata Test", TestBatchAddWithMetadataAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Add Empty List Test", TestBatchAddEmptyListAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Add Single Document Test", TestBatchAddSingleDocumentAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Add Multiple Documents Test", TestBatchAddMultipleDocumentsAsync).ConfigureAwait(false);
        }

        private static async Task TestBatchAddAllSuccessAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Create batch of documents
            List<BatchAddDocumentItem> documents = new List<BatchAddDocumentItem>
            {
                new BatchAddDocumentItem("doc1.txt", "The quick brown fox jumps over the lazy dog."),
                new BatchAddDocumentItem("doc2.txt", "Machine learning is transforming technology."),
                new BatchAddDocumentItem("doc3.txt", "Natural language processing advances rapidly.")
            };

            // Add batch
            BatchAddDocumentsResponse result = await index.AddDocumentsBatchAsync(documents).ConfigureAwait(false);

            // Verify all added successfully
            TestAssert.AreEqual(3, result.AddedCount);
            TestAssert.AreEqual(0, result.FailedCount);

            // Verify each document was added
            HashSet<string> addedNames = result.Added.Select(a => a.Name).ToHashSet();
            TestAssert.IsTrue(addedNames.Contains("doc1.txt"));
            TestAssert.IsTrue(addedNames.Contains("doc2.txt"));
            TestAssert.IsTrue(addedNames.Contains("doc3.txt"));

            // Verify documents exist in index
            long docCount = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(3, docCount);
        }

        private static async Task TestBatchAddWithCustomIdsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string customId1 = "custom-id-001";
            string customId2 = "custom-id-002";

            // Create batch with custom IDs
            List<BatchAddDocumentItem> documents = new List<BatchAddDocumentItem>
            {
                new BatchAddDocumentItem("doc1.txt", "First document content") { Id = customId1 },
                new BatchAddDocumentItem("doc2.txt", "Second document content") { Id = customId2 }
            };

            // Add batch
            BatchAddDocumentsResponse result = await index.AddDocumentsBatchAsync(documents).ConfigureAwait(false);

            // Verify custom IDs were used
            TestAssert.AreEqual(2, result.AddedCount);
            TestAssert.AreEqual(0, result.FailedCount);

            BatchAddDocumentResult firstAdded = result.Added.First(a => a.Name == "doc1.txt");
            TestAssert.AreEqual(customId1, firstAdded.DocumentId);

            BatchAddDocumentResult secondAdded = result.Added.First(a => a.Name == "doc2.txt");
            TestAssert.AreEqual(customId2, secondAdded.DocumentId);

            // Verify documents can be retrieved by custom ID
            DocumentMetadata? doc1 = await index.GetDocumentAsync(customId1).ConfigureAwait(false);
            TestAssert.IsNotNull(doc1);
            TestAssert.AreEqual("doc1.txt", doc1!.DocumentPath);
        }

        private static async Task TestBatchAddWithDuplicateCustomIdsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string existingId = "existing-id-001";
            string duplicatedId = "duplicate-id-001";

            await index.AddDocumentAsync(existingId, "existing-original.txt", "Existing document content").ConfigureAwait(false);

            List<BatchAddDocumentItem> documents = new List<BatchAddDocumentItem>
            {
                new BatchAddDocumentItem("existing-duplicate.txt", "Should fail because the ID already exists") { Id = existingId },
                new BatchAddDocumentItem("new-document.txt", "Should be added successfully") { Id = duplicatedId },
                new BatchAddDocumentItem("request-duplicate.txt", "Should fail because the ID is repeated") { Id = duplicatedId }
            };

            BatchAddDocumentsResponse result = await index.AddDocumentsBatchAsync(documents).ConfigureAwait(false);

            TestAssert.AreEqual(1, result.AddedCount);
            TestAssert.AreEqual(2, result.FailedCount);

            TestAssert.AreEqual(duplicatedId, result.Added[0].DocumentId);

            BatchAddDocumentResult existingFailure = result.Failed.First(f => f.Name == "existing-duplicate.txt");
            TestAssert.AreEqual($"Document with ID '{existingId}' already exists.", existingFailure.ErrorMessage);

            BatchAddDocumentResult requestDuplicateFailure = result.Failed.First(f => f.Name == "request-duplicate.txt");
            TestAssert.AreEqual($"Duplicate document ID '{duplicatedId}' in batch.", requestDuplicateFailure.ErrorMessage);
        }

        private static async Task TestBatchAddWithMetadataAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Create batch with labels and tags
            List<string> labels1 = new List<string> { "important", "urgent" };
            Dictionary<string, string> tags1 = new Dictionary<string, string> { { "category", "technical" }, { "author", "test" } };

            List<BatchAddDocumentItem> documents = new List<BatchAddDocumentItem>
            {
                new BatchAddDocumentItem("doc1.txt", "Document with full metadata") { Labels = labels1, Tags = tags1 },
                new BatchAddDocumentItem("doc2.txt", "Document without metadata")
            };

            // Add batch
            BatchAddDocumentsResponse result = await index.AddDocumentsBatchAsync(documents).ConfigureAwait(false);

            TestAssert.AreEqual(2, result.AddedCount);
            TestAssert.AreEqual(0, result.FailedCount);

            // Verify metadata was added
            string docId1 = result.Added.First(a => a.Name == "doc1.txt").DocumentId;
            DocumentMetadata? doc1 = await index.GetDocumentWithMetadataAsync(docId1).ConfigureAwait(false);

            TestAssert.IsNotNull(doc1);
            TestAssert.IsNotNull(doc1!.Labels);
            TestAssert.AreEqual(2, doc1.Labels!.Count);
            TestAssert.IsTrue(doc1.Labels.Contains("important"));
            TestAssert.IsTrue(doc1.Labels.Contains("urgent"));

            TestAssert.IsNotNull(doc1.Tags);
            TestAssert.AreEqual(2, doc1.Tags!.Count);
            TestAssert.AreEqual("technical", doc1.Tags["category"]);
            TestAssert.AreEqual("test", doc1.Tags["author"]);
        }

        private static async Task TestBatchAddEmptyListAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add empty batch
            List<BatchAddDocumentItem> documents = new List<BatchAddDocumentItem>();

            BatchAddDocumentsResponse result = await index.AddDocumentsBatchAsync(documents).ConfigureAwait(false);

            // Should return empty results
            TestAssert.AreEqual(0, result.AddedCount);
            TestAssert.AreEqual(0, result.FailedCount);

            // Index should have no documents
            long docCount = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(0, docCount);
        }

        private static async Task TestBatchAddSingleDocumentAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add batch with single document
            List<BatchAddDocumentItem> documents = new List<BatchAddDocumentItem>
            {
                new BatchAddDocumentItem("single.txt", "Single document in batch")
            };

            BatchAddDocumentsResponse result = await index.AddDocumentsBatchAsync(documents).ConfigureAwait(false);

            TestAssert.AreEqual(1, result.AddedCount);
            TestAssert.AreEqual(0, result.FailedCount);
            TestAssert.AreEqual("single.txt", result.Added[0].Name);
        }

        private static async Task TestBatchAddMultipleDocumentsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add batch with many documents
            List<BatchAddDocumentItem> documents = new List<BatchAddDocumentItem>();

            for (int i = 0; i < 10; i++)
            {
                documents.Add(new BatchAddDocumentItem($"doc{i}.txt", $"Content for document number {i}"));
            }

            BatchAddDocumentsResponse result = await index.AddDocumentsBatchAsync(documents).ConfigureAwait(false);

            TestAssert.AreEqual(10, result.AddedCount);
            TestAssert.AreEqual(0, result.FailedCount);

            // Verify all documents exist
            long docCount = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(10, docCount);
        }
    }
}
