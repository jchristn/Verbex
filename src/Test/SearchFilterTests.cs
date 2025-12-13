namespace Test
{
    using System;
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
    }
}
