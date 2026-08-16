namespace Test.Shared
{
    using System;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Negative and edge-case tests for the <see cref="InvertedIndex"/> public API.
    /// These exercise argument-validation guards, the "index not open" guard, and
    /// boundary behaviors (empty content, re-adding a removed document, explicit
    /// document identifiers) that the positive-path suites do not cover.
    /// </summary>
    public static class NegativeValidationTests
    {
        /// <summary>
        /// Runs all negative and edge-case validation tests.
        /// </summary>
        /// <param name="runner">Test collector used to register the test cases.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(ITestCollector runner)
        {
            // Constructor guards
            await runner.RunTestAsync("Constructor Rejects Null Index Name", TestConstructorRejectsNullNameAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Constructor Rejects Empty Index Name", TestConstructorRejectsEmptyNameAsync).ConfigureAwait(false);

            // Not-open guard
            await runner.RunTestAsync("Operation Before Open Throws", TestOperationBeforeOpenThrowsAsync).ConfigureAwait(false);

            // Document argument guards
            await runner.RunTestAsync("AddDocument Rejects Null Name", TestAddDocumentRejectsNullNameAsync).ConfigureAwait(false);
            await runner.RunTestAsync("AddDocument Rejects Null Content", TestAddDocumentRejectsNullContentAsync).ConfigureAwait(false);
            await runner.RunTestAsync("AddDocument With Explicit Id Rejects Empty Id", TestAddDocumentRejectsEmptyIdAsync).ConfigureAwait(false);
            await runner.RunTestAsync("GetDocument Rejects Null Id", TestGetDocumentRejectsNullIdAsync).ConfigureAwait(false);
            await runner.RunTestAsync("RemoveDocument Rejects Null Id", TestRemoveDocumentRejectsNullIdAsync).ConfigureAwait(false);

            // Label and tag argument guards
            await runner.RunTestAsync("AddLabel Rejects Null Document Id", TestAddLabelRejectsNullDocumentIdAsync).ConfigureAwait(false);
            await runner.RunTestAsync("AddLabel Rejects Null Label", TestAddLabelRejectsNullLabelAsync).ConfigureAwait(false);
            await runner.RunTestAsync("SetTag Rejects Null Key", TestSetTagRejectsNullKeyAsync).ConfigureAwait(false);

            // Positive edge cases
            await runner.RunTestAsync("Add Document With Empty Content Succeeds", TestAddDocumentWithEmptyContentAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Re-add After Removal Succeeds", TestReaddAfterRemovalAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Add Document With Explicit Id Is Retrievable", TestAddDocumentWithExplicitIdAsync).ConfigureAwait(false);
        }

        private static Task TestConstructorRejectsNullNameAsync()
        {
            TestAssert.Throws<ArgumentNullException>(() => new InvertedIndex(null!));
            return Task.CompletedTask;
        }

        private static Task TestConstructorRejectsEmptyNameAsync()
        {
            TestAssert.Throws<ArgumentException>(() => new InvertedIndex("   "));
            return Task.CompletedTask;
        }

        private static async Task TestOperationBeforeOpenThrowsAsync()
        {
            VerbexConfiguration config = TestContext.CreateTestConfiguration();

            await using InvertedIndex index = new InvertedIndex($"unopened_{Guid.NewGuid():N}", config);

            // The index has been constructed but OpenAsync was never called.
            await TestAssert.ThrowsAsync<InvalidOperationException>(
                () => index.GetDocumentCountAsync()).ConfigureAwait(false);
        }

        private static async Task TestAddDocumentRejectsNullNameAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await TestAssert.ThrowsAsync<ArgumentNullException>(
                () => index.AddDocumentAsync(null!, "content")).ConfigureAwait(false);
        }

        private static async Task TestAddDocumentRejectsNullContentAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await TestAssert.ThrowsAsync<ArgumentNullException>(
                () => index.AddDocumentAsync("doc.txt", null!)).ConfigureAwait(false);
        }

        private static async Task TestAddDocumentRejectsEmptyIdAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await TestAssert.ThrowsAsync<ArgumentException>(
                () => index.AddDocumentAsync("   ", "doc.txt", "content")).ConfigureAwait(false);
        }

        private static async Task TestGetDocumentRejectsNullIdAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await TestAssert.ThrowsAsync<ArgumentNullException>(
                () => index.GetDocumentAsync(null!)).ConfigureAwait(false);
        }

        private static async Task TestRemoveDocumentRejectsNullIdAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await TestAssert.ThrowsAsync<ArgumentNullException>(
                () => index.RemoveDocumentAsync(null!)).ConfigureAwait(false);
        }

        private static async Task TestAddLabelRejectsNullDocumentIdAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await TestAssert.ThrowsAsync<ArgumentNullException>(
                () => index.AddLabelAsync(null!, "label")).ConfigureAwait(false);
        }

        private static async Task TestAddLabelRejectsNullLabelAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("doc.txt", "some content").ConfigureAwait(false);

            await TestAssert.ThrowsAsync<ArgumentNullException>(
                () => index.AddLabelAsync(docId, null!)).ConfigureAwait(false);
        }

        private static async Task TestSetTagRejectsNullKeyAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("doc.txt", "some content").ConfigureAwait(false);

            await TestAssert.ThrowsAsync<ArgumentNullException>(
                () => index.SetTagAsync(docId, null!, "value")).ConfigureAwait(false);
        }

        private static async Task TestAddDocumentWithEmptyContentAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            // Empty (but non-null) content is a valid document with no indexable terms.
            string docId = await index.AddDocumentAsync("empty.txt", string.Empty).ConfigureAwait(false);

            TestAssert.AreEqual(1L, await index.GetDocumentCountAsync().ConfigureAwait(false));
            TestAssert.IsTrue(await index.DocumentExistsAsync(docId).ConfigureAwait(false));

            // A document with no terms cannot be matched by any query.
            SearchResults results = await index.SearchAsync("anything").ConfigureAwait(false);
            TestAssert.AreEqual(0, results.TotalCount);
        }

        private static async Task TestReaddAfterRemovalAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string firstId = await index.AddDocumentAsync("doc.txt", "recyclable content").ConfigureAwait(false);
            TestAssert.IsTrue(await index.RemoveDocumentAsync(firstId).ConfigureAwait(false));
            TestAssert.AreEqual(0L, await index.GetDocumentCountAsync().ConfigureAwait(false));

            string secondId = await index.AddDocumentAsync("doc.txt", "recyclable content").ConfigureAwait(false);
            TestAssert.AreEqual(1L, await index.GetDocumentCountAsync().ConfigureAwait(false));
            TestAssert.IsTrue(await index.DocumentExistsAsync(secondId).ConfigureAwait(false));

            SearchResults results = await index.SearchAsync("recyclable").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);
            TestAssert.AreEqual(secondId, results.Results[0].DocumentId);
        }

        private static async Task TestAddDocumentWithExplicitIdAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string explicitId = "explicit-doc-id-001";
            await index.AddDocumentAsync(explicitId, "doc.txt", "hello explicit world").ConfigureAwait(false);

            TestAssert.IsTrue(await index.DocumentExistsAsync(explicitId).ConfigureAwait(false));

            DocumentMetadata? metadata = await index.GetDocumentAsync(explicitId).ConfigureAwait(false);
            TestAssert.IsNotNull(metadata);
            TestAssert.AreEqual(explicitId, metadata!.DocumentId);

            SearchResults results = await index.SearchAsync("explicit").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);
            TestAssert.AreEqual(explicitId, results.Results[0].DocumentId);
        }
    }
}
