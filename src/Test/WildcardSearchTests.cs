namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for wildcard "*" search functionality.
    /// </summary>
    public static class WildcardSearchTests
    {
        /// <summary>
        /// Runs all wildcard search tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("WildcardSearch_ReturnsAllDocuments", TestWildcardSearchReturnsAllDocumentsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("WildcardSearch_WithLabelFilter", TestWildcardSearchWithLabelFilterAsync).ConfigureAwait(false);
            await runner.RunTestAsync("WildcardSearch_WithTagFilter", TestWildcardSearchWithTagFilterAsync).ConfigureAwait(false);
            await runner.RunTestAsync("WildcardSearch_WithReportedUserMasterGuidTagFilter", TestWildcardSearchWithReportedUserMasterGuidTagFilterAsync).ConfigureAwait(false);
            await runner.RunTestAsync("WildcardSearch_WithMultipleTagFilters", TestWildcardSearchWithMultipleTagFiltersAsync).ConfigureAwait(false);
            await runner.RunTestAsync("WildcardSearch_WithMixedCaseLabelFilter", TestWildcardSearchWithMixedCaseLabelFilterAsync).ConfigureAwait(false);
            await runner.RunTestAsync("WildcardSearch_WithLabelAndTagFilter", TestWildcardSearchWithLabelAndTagFilterAsync).ConfigureAwait(false);
            await runner.RunTestAsync("WildcardSearch_MaxResults", TestWildcardSearchMaxResultsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("WildcardSearch_ReturnsZeroScores", TestWildcardSearchReturnsZeroScoresAsync).ConfigureAwait(false);
            await runner.RunTestAsync("WildcardSearch_TimingInfo", TestWildcardSearchTimingInfoAsync).ConfigureAwait(false);
        }

        private static async Task TestWildcardSearchReturnsAllDocumentsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "first document content").ConfigureAwait(false);
            await index.AddDocumentAsync("doc2.txt", "second document content").ConfigureAwait(false);
            await index.AddDocumentAsync("doc3.txt", "third document content").ConfigureAwait(false);

            SearchResults results = await index.SearchAsync("*").ConfigureAwait(false);

            TestAssert.AreEqual(3, results.TotalCount, "Wildcard should return all 3 documents");
            TestAssert.AreEqual(3, results.Results.Count, "Wildcard results count should be 3");
        }

        private static async Task TestWildcardSearchWithLabelFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "first document").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "second document").ConfigureAwait(false);
            string doc3 = await index.AddDocumentAsync("doc3.txt", "third document").ConfigureAwait(false);

            await index.AddLabelAsync(doc1, "featured").ConfigureAwait(false);
            await index.AddLabelAsync(doc2, "featured").ConfigureAwait(false);

            List<string> labels = new List<string> { "featured" };
            SearchResults results = await index.SearchAsync("*", null, false, labels, null).ConfigureAwait(false);

            TestAssert.AreEqual(2, results.TotalCount, "Wildcard with label filter should return 2 documents");
        }

        private static async Task TestWildcardSearchWithTagFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "first document").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "second document").ConfigureAwait(false);
            string doc3 = await index.AddDocumentAsync("doc3.txt", "third document").ConfigureAwait(false);

            await index.SetTagAsync(doc1, "status", "published").ConfigureAwait(false);
            await index.SetTagAsync(doc3, "status", "published").ConfigureAwait(false);

            Dictionary<string, string> tags = new Dictionary<string, string> { { "status", "published" } };
            SearchResults results = await index.SearchAsync("*", null, false, null, tags).ConfigureAwait(false);

            TestAssert.AreEqual(2, results.TotalCount, "Wildcard with tag filter should return 2 documents");
        }

        private static async Task TestWildcardSearchWithReportedUserMasterGuidTagFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string matchingUserMasterGuid = "292799b6-6a32-4098-b472-972ab4cc0897";
            string doc1 = await index.AddDocumentAsync("doc1.txt", "first document").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "second document").ConfigureAwait(false);

            await index.SetTagAsync(doc1, "UserMasterGUID", matchingUserMasterGuid).ConfigureAwait(false);
            await index.SetTagAsync(doc2, "UserMasterGUID", "a1ccfe04-23f1-4cfb-a019-2ec6ab641c37").ConfigureAwait(false);

            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "UserMasterGUID", matchingUserMasterGuid }
            };
            SearchResults results = await index.SearchAsync("*", 25, false, null, tags).ConfigureAwait(false);

            TestAssert.AreEqual(1, results.TotalCount, "Wildcard with UserMasterGUID tag filter should return 1 document");
            TestAssert.AreEqual(doc1, results.Results[0].DocumentId, "Wildcard tag filter should return the matching document");
        }

        private static async Task TestWildcardSearchWithMultipleTagFiltersAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "first document").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "second document").ConfigureAwait(false);
            string doc3 = await index.AddDocumentAsync("doc3.txt", "third document").ConfigureAwait(false);

            await index.AddTagsBatchAsync(doc1, new Dictionary<string, string> { { "status", "published" }, { "owner", "alpha" } }).ConfigureAwait(false);
            await index.AddTagsBatchAsync(doc2, new Dictionary<string, string> { { "status", "published" }, { "owner", "beta" } }).ConfigureAwait(false);
            await index.AddTagsBatchAsync(doc3, new Dictionary<string, string> { { "status", "draft" }, { "owner", "alpha" } }).ConfigureAwait(false);

            Dictionary<string, string> tags = new Dictionary<string, string>
            {
                { "status", "published" },
                { "owner", "alpha" }
            };
            SearchResults results = await index.SearchAsync("*", null, false, null, tags).ConfigureAwait(false);

            TestAssert.AreEqual(1, results.TotalCount, "Wildcard with multiple tag filters should use AND logic");
            TestAssert.AreEqual(doc1, results.Results[0].DocumentId, "Wildcard multiple tag filters should return the matching document");
        }

        private static async Task TestWildcardSearchWithMixedCaseLabelFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "first document").ConfigureAwait(false);
            await index.AddDocumentAsync("doc2.txt", "second document").ConfigureAwait(false);

            await index.AddLabelAsync(doc1, "Featured").ConfigureAwait(false);

            List<string> labels = new List<string> { "FEATURED" };
            SearchResults results = await index.SearchAsync("*", null, false, labels, null).ConfigureAwait(false);

            TestAssert.AreEqual(1, results.TotalCount, "Wildcard label filters should be case-insensitive");
            TestAssert.AreEqual(doc1, results.Results[0].DocumentId, "Wildcard mixed-case label filter should return the matching document");
        }

        private static async Task TestWildcardSearchWithLabelAndTagFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "first document").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "second document").ConfigureAwait(false);
            string doc3 = await index.AddDocumentAsync("doc3.txt", "third document").ConfigureAwait(false);

            await index.AddLabelAsync(doc1, "featured").ConfigureAwait(false);
            await index.AddLabelAsync(doc2, "featured").ConfigureAwait(false);
            await index.SetTagAsync(doc1, "status", "published").ConfigureAwait(false);
            await index.SetTagAsync(doc2, "status", "draft").ConfigureAwait(false);

            List<string> labels = new List<string> { "featured" };
            Dictionary<string, string> tags = new Dictionary<string, string> { { "status", "published" } };
            SearchResults results = await index.SearchAsync("*", null, false, labels, tags).ConfigureAwait(false);

            TestAssert.AreEqual(1, results.TotalCount, "Wildcard with both filters should return 1 document");
        }

        private static async Task TestWildcardSearchMaxResultsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            for (int i = 0; i < 10; i++)
            {
                await index.AddDocumentAsync($"doc{i}.txt", $"document content {i}").ConfigureAwait(false);
            }

            SearchResults results = await index.SearchAsync("*", maxResults: 3).ConfigureAwait(false);

            TestAssert.AreEqual(3, results.Results.Count, "Wildcard should respect maxResults limit");
            TestAssert.AreEqual(10, results.TotalCount, "TotalCount should reflect all matching documents");
        }

        private static async Task TestWildcardSearchReturnsZeroScoresAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "first document").ConfigureAwait(false);
            await index.AddDocumentAsync("doc2.txt", "second document").ConfigureAwait(false);

            SearchResults results = await index.SearchAsync("*").ConfigureAwait(false);

            foreach (SearchResult result in results.Results)
            {
                TestAssert.AreEqual(0.0, result.Score, "Wildcard search results should have score 0");
            }
        }

        private static async Task TestWildcardSearchTimingInfoAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "first document").ConfigureAwait(false);

            SearchResults results = await index.SearchAsync("*").ConfigureAwait(false);

            TestAssert.IsNotNull(results.TimingInfo, "Wildcard search should have timing info");
            TestAssert.IsTrue(results.SearchTime.TotalMilliseconds >= 0, "Search time should be non-negative");
        }
    }
}
