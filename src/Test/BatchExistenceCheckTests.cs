namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex;
    using Verbex.DTO.Responses;

    /// <summary>
    /// Tests for batch document existence check functionality.
    /// </summary>
    public static class BatchExistenceCheckTests
    {
        /// <summary>
        /// Runs all batch existence check tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("Batch Exists All Found Test", TestBatchExistsAllFoundAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Exists Partial Found Test", TestBatchExistsPartialFoundAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Exists None Found Test", TestBatchExistsNoneFoundAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Exists Empty List Test", TestBatchExistsEmptyListAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Exists With Duplicates Test", TestBatchExistsWithDuplicatesAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Exists Single Document Test", TestBatchExistsSingleDocumentAsync).ConfigureAwait(false);
        }

        private static async Task TestBatchExistsAllFoundAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add documents
            string docId1 = await index.AddDocumentAsync("doc1.txt", "First document content").ConfigureAwait(false);
            string docId2 = await index.AddDocumentAsync("doc2.txt", "Second document content").ConfigureAwait(false);
            string docId3 = await index.AddDocumentAsync("doc3.txt", "Third document content").ConfigureAwait(false);

            // Check all documents exist
            List<string> idsToCheck = new List<string> { docId1, docId2, docId3 };
            BatchCheckExistenceResponse result = await index.DocumentsExistBatchAsync(idsToCheck).ConfigureAwait(false);

            // Verify all exist
            TestAssert.AreEqual(3, result.ExistsCount);
            TestAssert.AreEqual(0, result.NotFoundCount);

            TestAssert.IsTrue(result.Exists.Contains(docId1));
            TestAssert.IsTrue(result.Exists.Contains(docId2));
            TestAssert.IsTrue(result.Exists.Contains(docId3));
        }

        private static async Task TestBatchExistsPartialFoundAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add documents
            string docId1 = await index.AddDocumentAsync("doc1.txt", "First document content").ConfigureAwait(false);
            string docId2 = await index.AddDocumentAsync("doc2.txt", "Second document content").ConfigureAwait(false);

            // Check existing and non-existing documents
            List<string> idsToCheck = new List<string> { docId1, "nonexistent-1", docId2, "nonexistent-2" };
            BatchCheckExistenceResponse result = await index.DocumentsExistBatchAsync(idsToCheck).ConfigureAwait(false);

            // Verify partial results
            TestAssert.AreEqual(2, result.ExistsCount);
            TestAssert.AreEqual(2, result.NotFoundCount);

            TestAssert.IsTrue(result.Exists.Contains(docId1));
            TestAssert.IsTrue(result.Exists.Contains(docId2));
            TestAssert.IsTrue(result.NotFound.Contains("nonexistent-1"));
            TestAssert.IsTrue(result.NotFound.Contains("nonexistent-2"));
        }

        private static async Task TestBatchExistsNoneFoundAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add a document so index is not empty
            await index.AddDocumentAsync("doc1.txt", "Some content").ConfigureAwait(false);

            // Check non-existing documents
            List<string> idsToCheck = new List<string> { "fake-id-1", "fake-id-2", "fake-id-3" };
            BatchCheckExistenceResponse result = await index.DocumentsExistBatchAsync(idsToCheck).ConfigureAwait(false);

            // Verify none exist
            TestAssert.AreEqual(0, result.ExistsCount);
            TestAssert.AreEqual(3, result.NotFoundCount);

            TestAssert.IsTrue(result.NotFound.Contains("fake-id-1"));
            TestAssert.IsTrue(result.NotFound.Contains("fake-id-2"));
            TestAssert.IsTrue(result.NotFound.Contains("fake-id-3"));
        }

        private static async Task TestBatchExistsEmptyListAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add a document
            await index.AddDocumentAsync("doc1.txt", "Some content").ConfigureAwait(false);

            // Check empty list
            List<string> idsToCheck = new List<string>();
            BatchCheckExistenceResponse result = await index.DocumentsExistBatchAsync(idsToCheck).ConfigureAwait(false);

            // Should return empty results
            TestAssert.AreEqual(0, result.ExistsCount);
            TestAssert.AreEqual(0, result.NotFoundCount);
        }

        private static async Task TestBatchExistsWithDuplicatesAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add a document
            string docId = await index.AddDocumentAsync("doc1.txt", "Document content").ConfigureAwait(false);

            // Check with duplicate IDs
            List<string> idsToCheck = new List<string> { docId, docId, docId };
            BatchCheckExistenceResponse result = await index.DocumentsExistBatchAsync(idsToCheck).ConfigureAwait(false);

            // Should deduplicate
            TestAssert.AreEqual(1, result.ExistsCount);
            TestAssert.AreEqual(0, result.NotFoundCount);
            TestAssert.AreEqual(docId, result.Exists[0]);
        }

        private static async Task TestBatchExistsSingleDocumentAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add a document
            string docId = await index.AddDocumentAsync("doc1.txt", "Document content").ConfigureAwait(false);

            // Check single document
            List<string> idsToCheck = new List<string> { docId };
            BatchCheckExistenceResponse result = await index.DocumentsExistBatchAsync(idsToCheck).ConfigureAwait(false);

            TestAssert.AreEqual(1, result.ExistsCount);
            TestAssert.AreEqual(0, result.NotFoundCount);
            TestAssert.AreEqual(docId, result.Exists[0]);

            // Check single non-existent
            idsToCheck = new List<string> { "nonexistent" };
            result = await index.DocumentsExistBatchAsync(idsToCheck).ConfigureAwait(false);

            TestAssert.AreEqual(0, result.ExistsCount);
            TestAssert.AreEqual(1, result.NotFoundCount);
        }
    }
}
