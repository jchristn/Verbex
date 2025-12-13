namespace Test
{
    using System;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for IDisposable/IAsyncDisposable functionality.
    /// </summary>
    public static class InvertedIndexDisposableTests
    {
        /// <summary>
        /// Runs all disposable tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("Using Statement Test", TestUsingStatementAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Async Dispose Test", TestAsyncDisposeAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Operations After Dispose Test", TestOperationsAfterDisposeAsync).ConfigureAwait(false);
        }

        private static async Task TestUsingStatementAsync()
        {
            string docId;
            {
                await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

                docId = await index.AddDocumentAsync("test.txt", "test content").ConfigureAwait(false);

                long count = await index.GetDocumentCountAsync().ConfigureAwait(false);
                TestAssert.AreEqual(1L, count);
            }

            // Index has been disposed, document id is still valid but index is gone
            TestAssert.IsFalse(string.IsNullOrEmpty(docId));
        }

        private static async Task TestAsyncDisposeAsync()
        {
            InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("test.txt", "test content").ConfigureAwait(false);

            long count = await index.GetDocumentCountAsync().ConfigureAwait(false);
            TestAssert.AreEqual(1L, count);

            await index.DisposeAsync().ConfigureAwait(false);

            // Index is now disposed
        }

        private static async Task TestOperationsAfterDisposeAsync()
        {
            InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddDocumentAsync("test.txt", "test content").ConfigureAwait(false);

            await index.DisposeAsync().ConfigureAwait(false);

            // Verify that operations throw after dispose
            bool threwException = false;
            try
            {
                await index.GetDocumentCountAsync().ConfigureAwait(false);
            }
            catch (ObjectDisposedException)
            {
                threwException = true;
            }

            TestAssert.IsTrue(threwException);
        }
    }
}
