namespace Test
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex;
    using Verbex.Models;

    /// <summary>
    /// Tests for opt-in search enrichment support.
    /// </summary>
    public static class SearchEnrichmentTests
    {
        /// <summary>
        /// Runs all search enrichment tests.
        /// </summary>
        /// <param name="runner">Test runner.</param>
        /// <returns>Task.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("Document term stats returns expected aggregates", TestDocumentTermStatsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Document term stats handles empty input", TestDocumentTermStatsEmptyInputAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Wildcard results can be enriched with document term stats", TestWildcardDocumentTermStatsAsync).ConfigureAwait(false);
        }

        private static async Task TestDocumentTermStatsAsync()
        {
            using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "alpha beta beta gamma").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "delta delta epsilon").ConfigureAwait(false);

            Dictionary<string, DocumentTermStats> stats = await index.GetDocumentTermStatsAsync(new[] { doc1, doc1, doc2, "missing" }).ConfigureAwait(false);

            TestAssert.AreEqual(2, stats.Count, "Duplicate IDs should not duplicate stats and missing IDs should be omitted");
            TestAssert.AreEqual(doc1, stats[doc1].DocumentId);
            TestAssert.AreEqual(3L, stats[doc1].UniqueTermCount);
            TestAssert.AreEqual(4L, stats[doc1].TotalTermOccurrences);
            TestAssert.AreEqual(2L, stats[doc2].UniqueTermCount);
            TestAssert.AreEqual(3L, stats[doc2].TotalTermOccurrences);
        }

        private static async Task TestDocumentTermStatsEmptyInputAsync()
        {
            using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            Dictionary<string, DocumentTermStats> stats = await index.GetDocumentTermStatsAsync(System.Array.Empty<string>()).ConfigureAwait(false);

            TestAssert.IsEmpty(stats, "Empty document ID input should return an empty dictionary");
        }

        private static async Task TestWildcardDocumentTermStatsAsync()
        {
            using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("doc1.txt", "wildcard stats stats").ConfigureAwait(false);
            SearchResults searchResults = await index.SearchAsync("*", 10).ConfigureAwait(false);

            TestAssert.IsTrue(searchResults.Results.Any(result => result.DocumentId == docId), "Wildcard search should include the indexed document");

            Dictionary<string, DocumentTermStats> stats = await index.GetDocumentTermStatsAsync(searchResults.Results.Select(result => result.DocumentId)).ConfigureAwait(false);

            TestAssert.IsTrue(stats.ContainsKey(docId), "Wildcard result should have document term stats available");
            TestAssert.AreEqual(2L, stats[docId].UniqueTermCount);
            TestAssert.AreEqual(3L, stats[docId].TotalTermOccurrences);
        }
    }
}
