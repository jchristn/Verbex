namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for labels and tags functionality on documents.
    /// </summary>
    public static class LabelsAndTagsTests
    {
        /// <summary>
        /// Runs all labels and tags tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("Add Label to Document", TestAddLabelAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Remove Label from Document", TestRemoveLabelAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Set Tag on Document", TestSetTagAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Remove Tag from Document", TestRemoveTagAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Index Level Labels", TestIndexLevelLabelsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Index Level Tags", TestIndexLevelTagsAsync).ConfigureAwait(false);
        }

        private static async Task TestAddLabelAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("test.txt", "test content").ConfigureAwait(false);

            await index.AddLabelAsync(docId, "important").ConfigureAwait(false);

            List<string> labels = await index.GetLabelsAsync(docId).ConfigureAwait(false);
            TestAssert.AreEqual(1, labels.Count);
            TestAssert.IsTrue(labels.Contains("important"));
        }

        private static async Task TestRemoveLabelAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("test.txt", "test content").ConfigureAwait(false);
            await index.AddLabelAsync(docId, "toremove").ConfigureAwait(false);

            bool removed = await index.RemoveLabelAsync(docId, "toremove").ConfigureAwait(false);
            TestAssert.IsTrue(removed);

            List<string> labels = await index.GetLabelsAsync(docId).ConfigureAwait(false);
            TestAssert.AreEqual(0, labels.Count);
        }

        private static async Task TestSetTagAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("test.txt", "test content").ConfigureAwait(false);

            await index.SetTagAsync(docId, "author", "Test Author").ConfigureAwait(false);

            Dictionary<string, string?> tags = await index.GetTagsAsync(docId).ConfigureAwait(false);
            TestAssert.AreEqual(1, tags.Count);
            TestAssert.AreEqual("Test Author", tags["author"]);
        }

        private static async Task TestRemoveTagAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            string docId = await index.AddDocumentAsync("test.txt", "test content").ConfigureAwait(false);
            await index.SetTagAsync(docId, "toremove", "value").ConfigureAwait(false);

            bool removed = await index.RemoveTagAsync(docId, "toremove").ConfigureAwait(false);
            TestAssert.IsTrue(removed);

            Dictionary<string, string?> tags = await index.GetTagsAsync(docId).ConfigureAwait(false);
            TestAssert.AreEqual(0, tags.Count);
        }

        private static async Task TestIndexLevelLabelsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.AddIndexLabelAsync("index-label").ConfigureAwait(false);

            List<string> labels = await index.GetIndexLabelsAsync().ConfigureAwait(false);
            TestAssert.AreEqual(1, labels.Count);
            TestAssert.IsTrue(labels.Contains("index-label"));

            bool removed = await index.RemoveIndexLabelAsync("index-label").ConfigureAwait(false);
            TestAssert.IsTrue(removed);

            labels = await index.GetIndexLabelsAsync().ConfigureAwait(false);
            TestAssert.AreEqual(0, labels.Count);
        }

        private static async Task TestIndexLevelTagsAsync()
        {
            await using InvertedIndex index = await TestContext.CreateTestIndexAsync().ConfigureAwait(false);

            await index.SetIndexTagAsync("version", "1.0").ConfigureAwait(false);

            Dictionary<string, string?> tags = await index.GetIndexTagsAsync().ConfigureAwait(false);
            TestAssert.AreEqual(1, tags.Count);
            TestAssert.AreEqual("1.0", tags["version"]);

            bool removed = await index.RemoveIndexTagAsync("version").ConfigureAwait(false);
            TestAssert.IsTrue(removed);

            tags = await index.GetIndexTagsAsync().ConfigureAwait(false);
            TestAssert.AreEqual(0, tags.Count);
        }
    }
}
