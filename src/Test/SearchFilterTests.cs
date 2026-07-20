namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for search filtering functionality.
    /// </summary>
    public static class SearchFilterTests
    {
        /// <summary>
        /// Runs all search filter tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("Basic Search Returns Results", TestBasicSearchReturnsResultsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Search With Max Results", TestSearchWithMaxResultsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Search With AND Logic", TestSearchWithAndLogicAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Search With Tag Filter", TestSearchWithTagFilterAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Search With Multiple Tag Filters", TestSearchWithMultipleTagFiltersAsync).ConfigureAwait(false);
        }

        private static async Task TestBasicSearchReturnsResultsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "hello world").ConfigureAwait(false);
            await index.AddDocumentAsync("doc2.txt", "hello there").ConfigureAwait(false);

            SearchResults results = await index.SearchAsync("hello").ConfigureAwait(false);
            TestAssert.AreEqual(2, results.TotalCount);
        }

        private static async Task TestSearchWithMaxResultsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            for (int i = 0; i < 10; i++)
            {
                await index.AddDocumentAsync($"doc{i}.txt", $"common term document {i}").ConfigureAwait(false);
            }

            SearchResults results = await index.SearchAsync("common", maxResults: 5).ConfigureAwait(false);
            TestAssert.AreEqual(5, results.Results.Count);
        }

        private static async Task TestSearchWithAndLogicAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "apple banana cherry").ConfigureAwait(false);
            await index.AddDocumentAsync("doc2.txt", "apple date").ConfigureAwait(false);
            await index.AddDocumentAsync("doc3.txt", "banana date").ConfigureAwait(false);

            // OR search - should find all with apple OR banana
            SearchResults orResults = await index.SearchAsync("apple banana").ConfigureAwait(false);
            TestAssert.AreEqual(3, orResults.TotalCount);

            // AND search - should find only doc1 with apple AND banana
            SearchResults andResults = await index.SearchAsync("apple banana", useAndLogic: true).ConfigureAwait(false);
            TestAssert.AreEqual(1, andResults.TotalCount);
        }

        private static async Task TestSearchWithTagFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "hello world").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "hello there").ConfigureAwait(false);

            await index.SetTagAsync(doc1, "UserMasterGUID", "292799b6-6a32-4098-b472-972ab4cc0897").ConfigureAwait(false);
            await index.SetTagAsync(doc2, "UserMasterGUID", "a1ccfe04-23f1-4cfb-a019-2ec6ab641c37").ConfigureAwait(false);

            SearchResults results = await index.SearchAsync(
                "hello",
                maxResults: 25,
                useAndLogic: false,
                labels: null,
                tags: new Dictionary<string, string>
                {
                    { "UserMasterGUID", "292799b6-6a32-4098-b472-972ab4cc0897" }
                }).ConfigureAwait(false);

            TestAssert.AreEqual(1, results.TotalCount, "Search with UserMasterGUID tag filter should return 1 document");
            TestAssert.AreEqual(doc1, results.Results[0].DocumentId, "Tag-filtered search should return the matching document");
        }

        private static async Task TestSearchWithMultipleTagFiltersAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "common search term").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "common search term").ConfigureAwait(false);
            string doc3 = await index.AddDocumentAsync("doc3.txt", "common search term").ConfigureAwait(false);

            await index.AddTagsBatchAsync(doc1, new Dictionary<string, string> { { "status", "published" }, { "owner", "alpha" } }).ConfigureAwait(false);
            await index.AddTagsBatchAsync(doc2, new Dictionary<string, string> { { "status", "published" }, { "owner", "beta" } }).ConfigureAwait(false);
            await index.AddTagsBatchAsync(doc3, new Dictionary<string, string> { { "status", "draft" }, { "owner", "alpha" } }).ConfigureAwait(false);

            SearchResults results = await index.SearchAsync(
                "common",
                maxResults: 25,
                useAndLogic: false,
                labels: null,
                tags: new Dictionary<string, string>
                {
                    { "status", "published" },
                    { "owner", "alpha" }
                }).ConfigureAwait(false);

            TestAssert.AreEqual(1, results.TotalCount, "Search with multiple tag filters should use AND logic");
            TestAssert.AreEqual(doc1, results.Results[0].DocumentId, "Search with multiple tag filters should return the matching document");
        }
    }
}
