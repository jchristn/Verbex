namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for updating labels and tags on existing documents.
    /// </summary>
    public static class LabelsAndTagsUpdateTests
    {
        /// <summary>
        /// Run all label and tag update tests.
        /// </summary>
        /// <param name="runner">Test runner instance.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("Update Document Labels", TestUpdateDocumentLabelsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Update Document Tags", TestUpdateDocumentTagsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Labels Persist After Retrieval", TestLabelsPersistAfterRetrievalAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Tags Persist After Retrieval", TestTagsPersistAfterRetrievalAsync).ConfigureAwait(false);
        }

        private static async Task TestUpdateDocumentLabelsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);
            string docId = await index.AddDocumentAsync("test.txt", "test content").ConfigureAwait(false);

            // Add initial labels
            await index.AddLabelAsync(docId, "original").ConfigureAwait(false);
            await index.AddLabelAsync(docId, "old-label").ConfigureAwait(false);

            List<string> labels = await index.GetLabelsAsync(docId).ConfigureAwait(false);
            TestAssert.AreEqual(2, labels.Count);

            // Remove old labels and add new ones
            await index.RemoveLabelAsync(docId, "original").ConfigureAwait(false);
            await index.RemoveLabelAsync(docId, "old-label").ConfigureAwait(false);
            await index.AddLabelAsync(docId, "new-label").ConfigureAwait(false);

            labels = await index.GetLabelsAsync(docId).ConfigureAwait(false);
            TestAssert.AreEqual(1, labels.Count);
            TestAssert.IsTrue(labels.Contains("new-label"));
        }

        private static async Task TestUpdateDocumentTagsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);
            string docId = await index.AddDocumentAsync("test.txt", "test content").ConfigureAwait(false);

            // Add initial tags
            await index.SetTagAsync(docId, "old-key", "old-value").ConfigureAwait(false);

            // Update tags
            await index.RemoveTagAsync(docId, "old-key").ConfigureAwait(false);
            await index.SetTagAsync(docId, "new-key", "new-value").ConfigureAwait(false);

            Dictionary<string, string?> tags = await index.GetTagsAsync(docId).ConfigureAwait(false);
            TestAssert.AreEqual(1, tags.Count);
            TestAssert.IsTrue(tags.ContainsKey("new-key"));
            TestAssert.AreEqual("new-value", tags["new-key"]);
        }

        private static async Task TestLabelsPersistAfterRetrievalAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);
            string docId = await index.AddDocumentAsync("test.txt", "test content").ConfigureAwait(false);

            await index.AddLabelAsync(docId, "persistent-label").ConfigureAwait(false);

            // Flush only for on-disk mode (in-memory requires target path)
            if (TestContext.CurrentStorageMode == StorageMode.OnDisk)
            {
                await index.FlushAsync().ConfigureAwait(false);
            }

            // Retrieve multiple times
            List<string> labels1 = await index.GetLabelsAsync(docId).ConfigureAwait(false);
            List<string> labels2 = await index.GetLabelsAsync(docId).ConfigureAwait(false);

            TestAssert.IsTrue(labels1.Contains("persistent-label"));
            TestAssert.IsTrue(labels2.Contains("persistent-label"));
        }

        private static async Task TestTagsPersistAfterRetrievalAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);
            string docId = await index.AddDocumentAsync("test.txt", "test content").ConfigureAwait(false);

            await index.SetTagAsync(docId, "persistent-key", "persistent-value").ConfigureAwait(false);

            // Flush only for on-disk mode (in-memory requires target path)
            if (TestContext.CurrentStorageMode == StorageMode.OnDisk)
            {
                await index.FlushAsync().ConfigureAwait(false);
            }

            // Retrieve multiple times
            Dictionary<string, string?> tags1 = await index.GetTagsAsync(docId).ConfigureAwait(false);
            Dictionary<string, string?> tags2 = await index.GetTagsAsync(docId).ConfigureAwait(false);

            TestAssert.IsTrue(tags1.ContainsKey("persistent-key"));
            TestAssert.IsTrue(tags2.ContainsKey("persistent-key"));
        }
    }
}
