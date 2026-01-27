namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for batch document retrieval functionality.
    /// </summary>
    public static class BatchDocumentRetrievalTests
    {
        /// <summary>
        /// Runs all batch document retrieval tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("All Documents Found Test", TestAllDocumentsFoundAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Partial Match Test", TestPartialMatchAsync).ConfigureAwait(false);
            await runner.RunTestAsync("None Found Test", TestNoneFoundAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Empty Input Test", TestEmptyInputAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Duplicate IDs Test", TestDuplicateIdsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Documents With Metadata Test", TestDocumentsWithMetadataAsync).ConfigureAwait(false);
        }

        private static async Task TestAllDocumentsFoundAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add multiple documents
            string docId1 = await index.AddDocumentAsync("doc1.txt", "The quick brown fox jumps over the lazy dog.").ConfigureAwait(false);
            string docId2 = await index.AddDocumentAsync("doc2.txt", "Machine learning is transforming technology.").ConfigureAwait(false);
            string docId3 = await index.AddDocumentAsync("doc3.txt", "Natural language processing advances rapidly.").ConfigureAwait(false);

            // Request all three documents
            List<string> requestedIds = new List<string> { docId1, docId2, docId3 };
            List<DocumentMetadata> results = await index.GetDocumentsWithMetadataAsync(requestedIds).ConfigureAwait(false);

            TestAssert.AreEqual(3, results.Count);

            HashSet<string> foundIds = results.Select(d => d.DocumentId).ToHashSet();
            TestAssert.IsTrue(foundIds.Contains(docId1));
            TestAssert.IsTrue(foundIds.Contains(docId2));
            TestAssert.IsTrue(foundIds.Contains(docId3));
        }

        private static async Task TestPartialMatchAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add two documents
            string docId1 = await index.AddDocumentAsync("doc1.txt", "Document one content").ConfigureAwait(false);
            string docId2 = await index.AddDocumentAsync("doc2.txt", "Document two content").ConfigureAwait(false);

            // Request existing documents plus non-existent ones
            List<string> requestedIds = new List<string>
            {
                docId1,
                "non-existent-id-12345",
                docId2,
                "another-fake-id-67890"
            };

            List<DocumentMetadata> results = await index.GetDocumentsWithMetadataAsync(requestedIds).ConfigureAwait(false);

            // Only the two existing documents should be returned
            TestAssert.AreEqual(2, results.Count);

            HashSet<string> foundIds = results.Select(d => d.DocumentId).ToHashSet();
            TestAssert.IsTrue(foundIds.Contains(docId1));
            TestAssert.IsTrue(foundIds.Contains(docId2));
            TestAssert.IsFalse(foundIds.Contains("non-existent-id-12345"));
            TestAssert.IsFalse(foundIds.Contains("another-fake-id-67890"));
        }

        private static async Task TestNoneFoundAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add a document
            await index.AddDocumentAsync("doc1.txt", "Some content").ConfigureAwait(false);

            // Request only non-existent documents
            List<string> requestedIds = new List<string>
            {
                "fake-id-1",
                "fake-id-2",
                "fake-id-3"
            };

            List<DocumentMetadata> results = await index.GetDocumentsWithMetadataAsync(requestedIds).ConfigureAwait(false);

            // No documents should be returned
            TestAssert.AreEqual(0, results.Count);
        }

        private static async Task TestEmptyInputAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add a document
            await index.AddDocumentAsync("doc1.txt", "Some content").ConfigureAwait(false);

            // Request with empty list
            List<string> requestedIds = new List<string>();
            List<DocumentMetadata> results = await index.GetDocumentsWithMetadataAsync(requestedIds).ConfigureAwait(false);

            // Should return empty list
            TestAssert.AreEqual(0, results.Count);
        }

        private static async Task TestDuplicateIdsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add a document
            string docId = await index.AddDocumentAsync("doc1.txt", "Document content").ConfigureAwait(false);

            // Request the same document ID multiple times
            List<string> requestedIds = new List<string>
            {
                docId,
                docId,
                docId
            };

            List<DocumentMetadata> results = await index.GetDocumentsWithMetadataAsync(requestedIds).ConfigureAwait(false);

            // Should only return the document once (deduplication)
            TestAssert.AreEqual(1, results.Count);
            TestAssert.AreEqual(docId, results[0].DocumentId);
        }

        private static async Task TestDocumentsWithMetadataAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add documents
            string docId1 = await index.AddDocumentAsync("doc1.txt", "Document with full metadata").ConfigureAwait(false);
            string docId2 = await index.AddDocumentAsync("doc2.txt", "Document with partial metadata").ConfigureAwait(false);

            // Add labels and tags to doc1
            await index.AddLabelAsync(docId1, "important").ConfigureAwait(false);
            await index.AddLabelAsync(docId1, "reviewed").ConfigureAwait(false);
            await index.SetTagAsync(docId1, "category", "technical").ConfigureAwait(false);
            await index.SetTagAsync(docId1, "author", "test").ConfigureAwait(false);

            // Add labels and tags to doc2
            await index.AddLabelAsync(docId2, "draft").ConfigureAwait(false);
            await index.SetTagAsync(docId2, "status", "pending").ConfigureAwait(false);

            // Retrieve both documents
            List<string> requestedIds = new List<string> { docId1, docId2 };
            List<DocumentMetadata> results = await index.GetDocumentsWithMetadataAsync(requestedIds).ConfigureAwait(false);

            TestAssert.AreEqual(2, results.Count);

            // Find and verify doc1
            DocumentMetadata? doc1 = results.FirstOrDefault(d => d.DocumentId == docId1);
            TestAssert.IsNotNull(doc1);
            TestAssert.IsNotNull(doc1!.Labels);
            TestAssert.IsNotNull(doc1.Tags);
            TestAssert.AreEqual(2, doc1.Labels!.Count);
            TestAssert.AreEqual(2, doc1.Tags!.Count);
            TestAssert.IsTrue(doc1.Labels.Contains("important"));
            TestAssert.IsTrue(doc1.Labels.Contains("reviewed"));
            TestAssert.IsTrue(doc1.Tags.ContainsKey("category"));
            TestAssert.AreEqual("technical", doc1.Tags["category"]);

            // Find and verify doc2
            DocumentMetadata? doc2 = results.FirstOrDefault(d => d.DocumentId == docId2);
            TestAssert.IsNotNull(doc2);
            TestAssert.IsNotNull(doc2!.Labels);
            TestAssert.IsNotNull(doc2.Tags);
            TestAssert.AreEqual(1, doc2.Labels!.Count);
            TestAssert.AreEqual(1, doc2.Tags!.Count);
            TestAssert.IsTrue(doc2.Labels.Contains("draft"));
            TestAssert.IsTrue(doc2.Tags.ContainsKey("status"));
            TestAssert.AreEqual("pending", doc2.Tags["status"]);
        }
    }
}
