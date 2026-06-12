namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex;
    using Verbex.DTO.Responses;

    /// <summary>
    /// Tests for batch document delete functionality.
    /// </summary>
    public static class BatchDeleteDocumentsTests
    {
        /// <summary>
        /// Runs all batch document delete tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("Batch Delete All Found Test", TestBatchDeleteAllFoundAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Delete Partial Found Test", TestBatchDeletePartialFoundAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Delete None Found Test", TestBatchDeleteNoneFoundAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Delete Empty List Test", TestBatchDeleteEmptyListAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Delete With Duplicates Test", TestBatchDeleteWithDuplicatesAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Delete Verifies Removal Test", TestBatchDeleteVerifiesRemovalAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Batch Delete Cascades Metadata Test", TestBatchDeleteCascadesMetadataAsync).ConfigureAwait(false);
        }

        private static async Task TestBatchDeleteAllFoundAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add documents
            string docId1 = await index.AddDocumentAsync("doc1.txt", "First document content").ConfigureAwait(false);
            string docId2 = await index.AddDocumentAsync("doc2.txt", "Second document content").ConfigureAwait(false);
            string docId3 = await index.AddDocumentAsync("doc3.txt", "Third document content").ConfigureAwait(false);

            // Delete all documents
            List<string> idsToDelete = new List<string> { docId1, docId2, docId3 };
            BatchDeleteResponse result = await index.RemoveDocumentsBatchAsync(idsToDelete).ConfigureAwait(false);

            // Verify all deleted
            TestAssert.AreEqual(3, result.DeletedCount);
            TestAssert.AreEqual(0, result.NotFoundCount);

            TestAssert.IsTrue(result.Deleted.Contains(docId1));
            TestAssert.IsTrue(result.Deleted.Contains(docId2));
            TestAssert.IsTrue(result.Deleted.Contains(docId3));

            // Verify documents are gone
            long docCount = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(0, docCount);
        }

        private static async Task TestBatchDeletePartialFoundAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add documents
            string docId1 = await index.AddDocumentAsync("doc1.txt", "First document content").ConfigureAwait(false);
            string docId2 = await index.AddDocumentAsync("doc2.txt", "Second document content").ConfigureAwait(false);

            // Delete existing and non-existing documents
            List<string> idsToDelete = new List<string> { docId1, "nonexistent-1", docId2, "nonexistent-2" };
            BatchDeleteResponse result = await index.RemoveDocumentsBatchAsync(idsToDelete).ConfigureAwait(false);

            // Verify partial results
            TestAssert.AreEqual(2, result.DeletedCount);
            TestAssert.AreEqual(2, result.NotFoundCount);

            TestAssert.IsTrue(result.Deleted.Contains(docId1));
            TestAssert.IsTrue(result.Deleted.Contains(docId2));
            TestAssert.IsTrue(result.NotFound.Contains("nonexistent-1"));
            TestAssert.IsTrue(result.NotFound.Contains("nonexistent-2"));
        }

        private static async Task TestBatchDeleteNoneFoundAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add a document so index is not empty
            await index.AddDocumentAsync("doc1.txt", "Some content").ConfigureAwait(false);

            // Delete non-existing documents
            List<string> idsToDelete = new List<string> { "fake-id-1", "fake-id-2", "fake-id-3" };
            BatchDeleteResponse result = await index.RemoveDocumentsBatchAsync(idsToDelete).ConfigureAwait(false);

            // Verify none deleted
            TestAssert.AreEqual(0, result.DeletedCount);
            TestAssert.AreEqual(3, result.NotFoundCount);

            // Original document should still exist
            long docCount = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(1, docCount);
        }

        private static async Task TestBatchDeleteEmptyListAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add a document
            await index.AddDocumentAsync("doc1.txt", "Some content").ConfigureAwait(false);

            // Delete empty list
            List<string> idsToDelete = new List<string>();
            BatchDeleteResponse result = await index.RemoveDocumentsBatchAsync(idsToDelete).ConfigureAwait(false);

            // Should return empty results
            TestAssert.AreEqual(0, result.DeletedCount);
            TestAssert.AreEqual(0, result.NotFoundCount);

            // Document should still exist
            long docCount = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(1, docCount);
        }

        private static async Task TestBatchDeleteWithDuplicatesAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add a document
            string docId = await index.AddDocumentAsync("doc1.txt", "Document content").ConfigureAwait(false);

            // Delete with duplicate IDs
            List<string> idsToDelete = new List<string> { docId, docId, docId };
            BatchDeleteResponse result = await index.RemoveDocumentsBatchAsync(idsToDelete).ConfigureAwait(false);

            // Should deduplicate and delete only once
            TestAssert.AreEqual(1, result.DeletedCount);
            TestAssert.AreEqual(0, result.NotFoundCount);

            // Document should be gone
            bool exists = await index.DocumentExistsAsync(docId).ConfigureAwait(false);
            TestAssert.IsFalse(exists);
        }

        private static async Task TestBatchDeleteVerifiesRemovalAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add multiple documents
            string docId1 = await index.AddDocumentAsync("doc1.txt", "Document one with searchable terms").ConfigureAwait(false);
            string docId2 = await index.AddDocumentAsync("doc2.txt", "Document two with other terms").ConfigureAwait(false);
            string docId3 = await index.AddDocumentAsync("doc3.txt", "Document three stays").ConfigureAwait(false);

            // Delete first two
            List<string> idsToDelete = new List<string> { docId1, docId2 };
            BatchDeleteResponse result = await index.RemoveDocumentsBatchAsync(idsToDelete).ConfigureAwait(false);

            TestAssert.AreEqual(2, result.DeletedCount);

            // Verify deleted documents don't exist
            bool exists1 = await index.DocumentExistsAsync(docId1).ConfigureAwait(false);
            bool exists2 = await index.DocumentExistsAsync(docId2).ConfigureAwait(false);
            bool exists3 = await index.DocumentExistsAsync(docId3).ConfigureAwait(false);

            TestAssert.IsFalse(exists1);
            TestAssert.IsFalse(exists2);
            TestAssert.IsTrue(exists3);

            // Verify only one document remains
            long docCount = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(1, docCount);
        }

        private static async Task TestBatchDeleteCascadesMetadataAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId1 = await index.AddDocumentAsync("doc1.txt", "Document one with metadata").ConfigureAwait(false);
            string docId2 = await index.AddDocumentAsync("doc2.txt", "Document two with metadata").ConfigureAwait(false);
            string docId3 = await index.AddDocumentAsync("doc3.txt", "Document three stays").ConfigureAwait(false);

            await index.AddLabelAsync(docId1, "delete-label").ConfigureAwait(false);
            await index.SetTagAsync(docId1, "delete-key", "delete-value").ConfigureAwait(false);
            await index.AddLabelAsync(docId2, "delete-label").ConfigureAwait(false);
            await index.SetTagAsync(docId2, "delete-key", "delete-value").ConfigureAwait(false);
            await index.AddLabelAsync(docId3, "keep-label").ConfigureAwait(false);
            await index.SetTagAsync(docId3, "keep-key", "keep-value").ConfigureAwait(false);

            BatchDeleteResponse result = await index.RemoveDocumentsBatchAsync(new List<string> { docId1, docId2 }).ConfigureAwait(false);

            TestAssert.AreEqual(2, result.DeletedCount);
            TestAssert.AreEqual(0, (await index.GetLabelsAsync(docId1).ConfigureAwait(false)).Count, "Batch delete should remove labels for first deleted document");
            TestAssert.AreEqual(0, (await index.GetTagsAsync(docId1).ConfigureAwait(false)).Count, "Batch delete should remove tags for first deleted document");
            TestAssert.AreEqual(0, (await index.GetLabelsAsync(docId2).ConfigureAwait(false)).Count, "Batch delete should remove labels for second deleted document");
            TestAssert.AreEqual(0, (await index.GetTagsAsync(docId2).ConfigureAwait(false)).Count, "Batch delete should remove tags for second deleted document");

            TestAssert.AreEqual(1, (await index.GetLabelsAsync(docId3).ConfigureAwait(false)).Count, "Batch delete should not remove labels for remaining documents");
            Dictionary<string, string> remainingTags = await index.GetTagsAsync(docId3).ConfigureAwait(false);
            TestAssert.AreEqual(1, remainingTags.Count, "Batch delete should not remove tags for remaining documents");
            TestAssert.AreEqual("keep-value", remainingTags["keep-key"]);
        }
    }
}
