namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for label and tag filtering in search operations.
    /// </summary>
    public static class MetadataFilterTests
    {
        /// <summary>
        /// Runs all metadata filter tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("Search with Label Filter", TestSearchWithLabelFilterAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Search with Tag Filter", TestSearchWithTagFilterAsync).ConfigureAwait(false);
        }

        private static async Task TestSearchWithLabelFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add documents with labels
            string doc1 = await index.AddDocumentAsync("doc1.txt", "machine learning algorithms").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "machine learning data").ConfigureAwait(false);

            // Add labels to documents
            await index.AddLabelAsync(doc1, "tech").ConfigureAwait(false);
            await index.AddLabelAsync(doc2, "science").ConfigureAwait(false);

            // Search for "machine" - should find both
            SearchResults allResults = await index.SearchAsync("machine").ConfigureAwait(false);
            TestAssert.AreEqual(2, allResults.TotalCount);

            // Verify labels were added
            List<string> doc1Labels = await index.GetLabelsAsync(doc1).ConfigureAwait(false);
            TestAssert.AreEqual(1, doc1Labels.Count);
            TestAssert.IsTrue(doc1Labels.Contains("tech"));
        }

        private static async Task TestSearchWithTagFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Add documents with tags
            string doc1 = await index.AddDocumentAsync("doc1.txt", "database query").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "database admin").ConfigureAwait(false);

            // Add tags to documents
            await index.SetTagAsync(doc1, "department", "engineering").ConfigureAwait(false);
            await index.SetTagAsync(doc2, "department", "operations").ConfigureAwait(false);

            // Verify tags were added
            Dictionary<string, string?> doc1Tags = await index.GetTagsAsync(doc1).ConfigureAwait(false);
            TestAssert.AreEqual(1, doc1Tags.Count);
            TestAssert.AreEqual("engineering", doc1Tags["department"]);
        }
    }
}
