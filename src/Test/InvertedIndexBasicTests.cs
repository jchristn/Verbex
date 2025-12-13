namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Basic tests for InvertedIndex functionality.
    /// </summary>
    public static class InvertedIndexBasicTests
    {
        /// <summary>
        /// Runs all basic tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("Document Addition Test", TestDocumentAdditionAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Document Removal Test", TestDocumentRemovalAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Basic Search Test", TestBasicSearchAsync).ConfigureAwait(false);
            await runner.RunTestAsync("AND Search Test", TestAndSearchAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Document Count Test", TestDocumentCountAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Flush Operation Test", TestFlushOperationAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Max Results Limit Test", TestMaxResultsLimitAsync).ConfigureAwait(false);
        }

        private static async Task TestDocumentAdditionAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("test.txt", "hello world test document").ConfigureAwait(false);

            long count = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(1L, count);
            TestAssert.IsTrue(await index.DocumentExistsAsync(docId).ConfigureAwait(false));

            SearchResults results = await index.SearchAsync("hello").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);
            TestAssert.AreEqual(docId, results.Results[0].DocumentId);
        }

        private static async Task TestDocumentRemovalAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("test.txt", "content to be removed").ConfigureAwait(false);
            long count = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(1L, count);

            bool removed = await index.RemoveDocumentAsync(docId).ConfigureAwait(false);
            TestAssert.IsTrue(removed);

            count = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(0L, count);
            TestAssert.IsFalse(await index.DocumentExistsAsync(docId).ConfigureAwait(false));

            SearchResults results = await index.SearchAsync("content").ConfigureAwait(false);
            TestAssert.AreEqual(0, results.TotalCount);
        }

        private static async Task TestBasicSearchAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId1 = await index.AddDocumentAsync("doc1.txt", "apple banana cherry").ConfigureAwait(false);
            string docId2 = await index.AddDocumentAsync("doc2.txt", "banana cherry date").ConfigureAwait(false);
            string docId3 = await index.AddDocumentAsync("doc3.txt", "cherry date elderberry").ConfigureAwait(false);

            SearchResults cherryResults = await index.SearchAsync("cherry").ConfigureAwait(false);
            TestAssert.AreEqual(3, cherryResults.TotalCount);
            TestHelpers.DisplaySearchResults(cherryResults, "cherry", "Basic Search - Cherry");

            SearchResults bananaResults = await index.SearchAsync("banana").ConfigureAwait(false);
            TestAssert.AreEqual(2, bananaResults.TotalCount);
            TestHelpers.DisplaySearchResults(bananaResults, "banana", "Basic Search - Banana");

            SearchResults appleResults = await index.SearchAsync("apple").ConfigureAwait(false);
            TestAssert.AreEqual(1, appleResults.TotalCount);
            TestAssert.AreEqual(docId1, appleResults.Results[0].DocumentId);
            TestHelpers.DisplaySearchResults(appleResults, "apple", "Basic Search - Apple");
        }

        private static async Task TestAndSearchAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId1 = await index.AddDocumentAsync("doc1.txt", "apple banana cherry").ConfigureAwait(false);
            await index.AddDocumentAsync("doc2.txt", "banana cherry date").ConfigureAwait(false);
            await index.AddDocumentAsync("doc3.txt", "apple date elderberry").ConfigureAwait(false);

            SearchResults results = await index.SearchAsync("apple banana", useAndLogic: true).ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);
            TestAssert.AreEqual(docId1, results.Results[0].DocumentId);
            TestHelpers.DisplaySearchResults(results, "apple banana", "AND Search - Apple AND Banana");

            SearchResults noResults = await index.SearchAsync("apple elderberry banana", useAndLogic: true).ConfigureAwait(false);
            TestAssert.AreEqual(0, noResults.TotalCount);
            TestHelpers.DisplaySearchResults(noResults, "apple elderberry banana", "AND Search - No Results Expected");
        }

        private static async Task TestDocumentCountAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            long count = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(0L, count);

            for (int i = 0; i < 10; i++)
            {
                await index.AddDocumentAsync($"doc{i}.txt", $"document {i}").ConfigureAwait(false);
                count = await index.GetDocumentCountAsync().ConfigureAwait(false);
                TestAssert.AreEqual((long)(i + 1), count);
            }

            string removeId = await index.AddDocumentAsync("remove.txt", "to be removed").ConfigureAwait(false);
            count = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(11L, count);

            await index.RemoveDocumentAsync(removeId).ConfigureAwait(false);
            count = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(10L, count);
        }

        private static async Task TestFlushOperationAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            for (int i = 0; i < 10; i++)
            {
                await index.AddDocumentAsync($"doc{i}.txt", $"document {i}").ConfigureAwait(false);
            }

            // For in-memory mode, flush requires a target path (saves a snapshot)
            // For on-disk mode, flush commits pending changes
            if (TestContext.CurrentStorageMode == StorageMode.OnDisk)
            {
                await index.FlushAsync().ConfigureAwait(false);
            }

            long count = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(10L, count);
        }

        private static async Task TestMaxResultsLimitAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Create many documents containing the same term
            for (int i = 0; i < 20; i++)
            {
                await index.AddDocumentAsync($"doc{i}.txt", $"test document number {i}").ConfigureAwait(false);
            }

            // Search with max results limit
            SearchResults results = await index.SearchAsync("test", maxResults: 5).ConfigureAwait(false);
            TestAssert.AreEqual(5, results.Results.Count);

            TestHelpers.DisplaySearchResults(results, "test", "Max Results Limit Test");
        }
    }
}
