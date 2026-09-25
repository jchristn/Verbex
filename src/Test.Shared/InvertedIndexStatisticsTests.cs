namespace Test.Shared
{
    using System;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for index statistics functionality.
    /// </summary>
    public static class InvertedIndexStatisticsTests
    {
        /// <summary>
        /// Runs all statistics tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(ITestCollector runner)
        {
            await runner.RunTestAsync("Empty Index Statistics Test", TestEmptyIndexStatisticsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Index Statistics After Adding Documents", TestStatisticsAfterAddingDocumentsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Term Statistics Test", TestTermStatisticsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Term Count Test", TestTermCountAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Posting Count And Average Terms Per Document", TestPostingCountAndAverageTermsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Posting Count Updates After Document Removal", TestPostingCountAfterRemovalAsync).ConfigureAwait(false);
        }

        private static async Task TestEmptyIndexStatisticsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            IndexStatistics stats = await index.GetStatisticsAsync().ConfigureAwait(false);

            TestAssert.IsNotNull(stats);
            TestAssert.AreEqual(0L, stats.DocumentCount);
            TestAssert.AreEqual(0L, stats.TermCount);
            TestAssert.AreEqual(0L, stats.PostingCount);
            TestAssert.AreEqual(0.0, stats.AverageTermsPerDocument);
        }

        private static async Task TestStatisticsAfterAddingDocumentsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "apple banana cherry").ConfigureAwait(false);
            await index.AddDocumentAsync("doc2.txt", "banana cherry date").ConfigureAwait(false);
            await index.AddDocumentAsync("doc3.txt", "cherry date elderberry").ConfigureAwait(false);

            IndexStatistics stats = await index.GetStatisticsAsync().ConfigureAwait(false);

            TestAssert.IsNotNull(stats);
            TestAssert.AreEqual(3L, stats.DocumentCount);
            TestAssert.IsTrue(stats.TermCount > 0);
        }

        private static async Task TestTermStatisticsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "hello world hello").ConfigureAwait(false);
            await index.AddDocumentAsync("doc2.txt", "hello there").ConfigureAwait(false);

            Verbex.Models.TermStatisticsResult? stats = await index.GetTermStatisticsAsync("hello").ConfigureAwait(false);

            TestAssert.IsNotNull(stats);
            TestAssert.AreEqual(2, stats!.DocumentFrequency);  // In 2 documents
            TestAssert.AreEqual(3, stats.TotalFrequency);      // 3 total occurrences
        }

        private static async Task TestTermCountAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "apple banana cherry").ConfigureAwait(false);

            long termCount = await index.GetTermCountAsync().ConfigureAwait(false);

            TestAssert.AreEqual(3L, termCount);  // 3 unique terms
        }

        private static async Task TestPostingCountAndAverageTermsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "apple banana cherry").ConfigureAwait(false);
            await index.AddDocumentAsync("doc2.txt", "banana cherry date").ConfigureAwait(false);
            await index.AddDocumentAsync("doc3.txt", "cherry date elderberry cherry").ConfigureAwait(false);

            IndexStatistics stats = await index.GetStatisticsAsync().ConfigureAwait(false);

            TestAssert.AreEqual(3L, stats.DocumentCount);
            TestAssert.AreEqual(5L, stats.TermCount);               // apple, banana, cherry, date, elderberry
            TestAssert.AreEqual(9L, stats.PostingCount);            // 3 unique terms in each of 3 documents
            TestAssert.AreEqual(10L, stats.TotalTermOccurrences);   // doc3 repeats 'cherry'
            TestAssert.AreEqual(3.0, stats.AverageTermsPerDocument);
            TestAssert.AreEqual(3.3333, stats.AverageDocumentLength);
        }

        private static async Task TestPostingCountAfterRemovalAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "apple banana").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "cherry date elderberry fig").ConfigureAwait(false);

            IndexStatistics before = await index.GetStatisticsAsync().ConfigureAwait(false);
            TestAssert.AreEqual(6L, before.PostingCount);
            TestAssert.AreEqual(3.0, before.AverageTermsPerDocument);

            await index.RemoveDocumentAsync(doc2).ConfigureAwait(false);

            IndexStatistics after = await index.GetStatisticsAsync().ConfigureAwait(false);
            TestAssert.AreEqual(1L, after.DocumentCount);
            TestAssert.AreEqual(2L, after.PostingCount);
            TestAssert.AreEqual(2.0, after.AverageTermsPerDocument);
        }
    }
}
