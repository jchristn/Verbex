namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for filtered document enumeration with labels and tags.
    /// </summary>
    public static class EnumerationFilterTests
    {
        /// <summary>
        /// Runs all enumeration filter tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("EnumerateDocuments_WithLabelFilter", TestEnumerateDocumentsWithLabelFilterAsync).ConfigureAwait(false);
            await runner.RunTestAsync("EnumerateDocuments_WithTagFilter", TestEnumerateDocumentsWithTagFilterAsync).ConfigureAwait(false);
            await runner.RunTestAsync("EnumerateDocuments_WithLabelAndTagFilter", TestEnumerateDocumentsWithLabelAndTagFilterAsync).ConfigureAwait(false);
            await runner.RunTestAsync("EnumerateDocuments_WithNoMatchingFilter", TestEnumerateDocumentsWithNoMatchingFilterAsync).ConfigureAwait(false);
            await runner.RunTestAsync("EnumerateDocuments_FilteredPagination", TestEnumerateDocumentsFilteredPaginationAsync).ConfigureAwait(false);
            await runner.RunTestAsync("EnumerateDocuments_FilteredCount", TestEnumerateDocumentsFilteredCountAsync).ConfigureAwait(false);
        }

        private static async Task TestEnumerateDocumentsWithLabelFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "first document content").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "second document content").ConfigureAwait(false);
            string doc3 = await index.AddDocumentAsync("doc3.txt", "third document content").ConfigureAwait(false);

            await index.AddLabelAsync(doc1, "important").ConfigureAwait(false);
            await index.AddLabelAsync(doc2, "important").ConfigureAwait(false);
            await index.AddLabelAsync(doc3, "archived").ConfigureAwait(false);

            List<string> labels = new List<string> { "important" };
            List<DocumentMetadata> results = await index.GetDocumentsAsync(100, 0, labels, null).ConfigureAwait(false);

            TestAssert.AreEqual(2, results.Count, "Should return 2 documents with 'important' label");
        }

        private static async Task TestEnumerateDocumentsWithTagFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "first document content").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "second document content").ConfigureAwait(false);
            string doc3 = await index.AddDocumentAsync("doc3.txt", "third document content").ConfigureAwait(false);

            await index.SetTagAsync(doc1, "category", "tech").ConfigureAwait(false);
            await index.SetTagAsync(doc2, "category", "tech").ConfigureAwait(false);
            await index.SetTagAsync(doc3, "category", "science").ConfigureAwait(false);

            Dictionary<string, string> tags = new Dictionary<string, string> { { "category", "tech" } };
            List<DocumentMetadata> results = await index.GetDocumentsAsync(100, 0, null, tags).ConfigureAwait(false);

            TestAssert.AreEqual(2, results.Count, "Should return 2 documents with category=tech tag");
        }

        private static async Task TestEnumerateDocumentsWithLabelAndTagFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "first document content").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "second document content").ConfigureAwait(false);
            string doc3 = await index.AddDocumentAsync("doc3.txt", "third document content").ConfigureAwait(false);

            await index.AddLabelAsync(doc1, "important").ConfigureAwait(false);
            await index.AddLabelAsync(doc2, "important").ConfigureAwait(false);
            await index.SetTagAsync(doc1, "category", "tech").ConfigureAwait(false);
            await index.SetTagAsync(doc2, "category", "science").ConfigureAwait(false);

            List<string> labels = new List<string> { "important" };
            Dictionary<string, string> tags = new Dictionary<string, string> { { "category", "tech" } };
            List<DocumentMetadata> results = await index.GetDocumentsAsync(100, 0, labels, tags).ConfigureAwait(false);

            TestAssert.AreEqual(1, results.Count, "Should return only doc1 with both important label and category=tech tag");
        }

        private static async Task TestEnumerateDocumentsWithNoMatchingFilterAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("doc1.txt", "first document content").ConfigureAwait(false);

            List<string> labels = new List<string> { "nonexistent" };
            List<DocumentMetadata> results = await index.GetDocumentsAsync(100, 0, labels, null).ConfigureAwait(false);

            TestAssert.AreEqual(0, results.Count, "Should return 0 documents when no labels match");
        }

        private static async Task TestEnumerateDocumentsFilteredPaginationAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            for (int i = 0; i < 10; i++)
            {
                string docId = await index.AddDocumentAsync($"doc{i}.txt", $"document content {i}").ConfigureAwait(false);
                await index.AddLabelAsync(docId, "paginated").ConfigureAwait(false);
            }

            List<string> labels = new List<string> { "paginated" };

            // First page
            List<DocumentMetadata> page1 = await index.GetDocumentsAsync(3, 0, labels, null).ConfigureAwait(false);
            TestAssert.AreEqual(3, page1.Count, "First page should have 3 documents");

            // Second page
            List<DocumentMetadata> page2 = await index.GetDocumentsAsync(3, 3, labels, null).ConfigureAwait(false);
            TestAssert.AreEqual(3, page2.Count, "Second page should have 3 documents");

            // Verify no overlap
            HashSet<string> page1Ids = new HashSet<string>(page1.Select(d => d.DocumentId));
            bool hasOverlap = page2.Any(d => page1Ids.Contains(d.DocumentId));
            TestAssert.IsFalse(hasOverlap, "Pages should not have overlapping documents");
        }

        private static async Task TestEnumerateDocumentsFilteredCountAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string doc1 = await index.AddDocumentAsync("doc1.txt", "first document").ConfigureAwait(false);
            string doc2 = await index.AddDocumentAsync("doc2.txt", "second document").ConfigureAwait(false);
            string doc3 = await index.AddDocumentAsync("doc3.txt", "third document").ConfigureAwait(false);

            await index.AddLabelAsync(doc1, "counted").ConfigureAwait(false);
            await index.AddLabelAsync(doc2, "counted").ConfigureAwait(false);

            List<string> labels = new List<string> { "counted" };
            long count = await index.GetDocumentCountAsync(labels, null).ConfigureAwait(false);

            TestAssert.AreEqual(2L, count, "Filtered count should be 2");

            long totalCount = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(3L, totalCount, "Total count should be 3");
        }
    }
}
