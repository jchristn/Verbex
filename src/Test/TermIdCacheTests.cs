namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for TermIdCache functionality.
    /// </summary>
    public static class TermIdCacheTests
    {
        /// <summary>
        /// Runs all TermIdCache tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("TermIdCache Basic Get/Set", TestBasicGetSetAsync).ConfigureAwait(false);
            await runner.RunTestAsync("TermIdCache TryGetId Returns False for Missing", TestTryGetIdReturnsFalseForMissingAsync).ConfigureAwait(false);
            await runner.RunTestAsync("TermIdCache TryGetIds Mixed Hits and Misses", TestTryGetIdsMixedAsync).ConfigureAwait(false);
            await runner.RunTestAsync("TermIdCache SetRange Updates Multiple", TestSetRangeAsync).ConfigureAwait(false);
            await runner.RunTestAsync("TermIdCache Load Replaces Content", TestLoadReplacesContentAsync).ConfigureAwait(false);
            await runner.RunTestAsync("TermIdCache Remove Operations", TestRemoveOperationsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("TermIdCache Clear Operations", TestClearOperationsAsync).ConfigureAwait(false);
            await runner.RunTestAsync("TermIdCache Dispose Behavior", TestDisposeBehaviorAsync).ConfigureAwait(false);
            await runner.RunTestAsync("TermIdCache Thread Safety", TestThreadSafetyAsync).ConfigureAwait(false);
            await runner.RunTestAsync("TermIdCache IsLoaded State", TestIsLoadedStateAsync).ConfigureAwait(false);
        }

        private static async Task TestBasicGetSetAsync()
        {
            using (TermIdCache cache = new TermIdCache("test_index"))
            {
                // Set a value
                cache.Set("hello", "term_id_1");

                // Get it back
                bool found = cache.TryGetId("hello", out string? termId);

                TestAssert.IsTrue(found);
                TestAssert.AreEqual("term_id_1", termId);
                TestAssert.AreEqual(1, cache.Count);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestTryGetIdReturnsFalseForMissingAsync()
        {
            using (TermIdCache cache = new TermIdCache("test_index"))
            {
                bool found = cache.TryGetId("nonexistent", out string? termId);

                TestAssert.IsFalse(found);
                TestAssert.IsNull(termId);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestTryGetIdsMixedAsync()
        {
            using (TermIdCache cache = new TermIdCache("test_index"))
            {
                // Add some terms
                cache.Set("apple", "id_1");
                cache.Set("banana", "id_2");
                cache.Set("cherry", "id_3");

                // Look up a mix of existing and non-existing terms
                List<string> terms = new List<string> { "apple", "banana", "missing1", "cherry", "missing2" };
                (Dictionary<string, string> found, List<string> notFound) = cache.TryGetIds(terms);

                TestAssert.AreEqual(3, found.Count);
                TestAssert.AreEqual("id_1", found["apple"]);
                TestAssert.AreEqual("id_2", found["banana"]);
                TestAssert.AreEqual("id_3", found["cherry"]);

                TestAssert.AreEqual(2, notFound.Count);
                TestAssert.IsTrue(notFound.Contains("missing1"));
                TestAssert.IsTrue(notFound.Contains("missing2"));
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestSetRangeAsync()
        {
            using (TermIdCache cache = new TermIdCache("test_index"))
            {
                Dictionary<string, string> termIds = new Dictionary<string, string>
                {
                    ["term1"] = "id_1",
                    ["term2"] = "id_2",
                    ["term3"] = "id_3"
                };

                cache.SetRange(termIds);

                TestAssert.AreEqual(3, cache.Count);

                cache.TryGetId("term1", out string? id1);
                cache.TryGetId("term2", out string? id2);
                cache.TryGetId("term3", out string? id3);

                TestAssert.AreEqual("id_1", id1);
                TestAssert.AreEqual("id_2", id2);
                TestAssert.AreEqual("id_3", id3);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestLoadReplacesContentAsync()
        {
            using (TermIdCache cache = new TermIdCache("test_index"))
            {
                // Add initial content
                cache.Set("old_term", "old_id");
                TestAssert.AreEqual(1, cache.Count);
                TestAssert.IsFalse(cache.IsLoaded);

                // Load new content (should replace)
                Dictionary<string, string> newTermIds = new Dictionary<string, string>
                {
                    ["new_term1"] = "new_id_1",
                    ["new_term2"] = "new_id_2"
                };
                cache.Load(newTermIds);

                // Old term should be gone
                bool oldFound = cache.TryGetId("old_term", out _);
                TestAssert.IsFalse(oldFound);

                // New terms should be present
                TestAssert.AreEqual(2, cache.Count);
                TestAssert.IsTrue(cache.IsLoaded);

                cache.TryGetId("new_term1", out string? newId1);
                TestAssert.AreEqual("new_id_1", newId1);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestRemoveOperationsAsync()
        {
            using (TermIdCache cache = new TermIdCache("test_index"))
            {
                cache.Set("term1", "id_1");
                cache.Set("term2", "id_2");
                cache.Set("term3", "id_3");

                TestAssert.AreEqual(3, cache.Count);

                // Remove single term
                bool removed = cache.Remove("term1");
                TestAssert.IsTrue(removed);
                TestAssert.AreEqual(2, cache.Count);
                TestAssert.IsFalse(cache.TryGetId("term1", out _));

                // Remove non-existent term
                removed = cache.Remove("nonexistent");
                TestAssert.IsFalse(removed);

                // Remove range
                int removedCount = cache.RemoveRange(new List<string> { "term2", "term3", "nonexistent" });
                TestAssert.AreEqual(2, removedCount);
                TestAssert.AreEqual(0, cache.Count);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestClearOperationsAsync()
        {
            using (TermIdCache cache = new TermIdCache("test_index"))
            {
                // Load some data
                Dictionary<string, string> termIds = new Dictionary<string, string>
                {
                    ["term1"] = "id_1",
                    ["term2"] = "id_2"
                };
                cache.Load(termIds);

                TestAssert.AreEqual(2, cache.Count);
                TestAssert.IsTrue(cache.IsLoaded);

                // Clear
                cache.Clear();

                TestAssert.AreEqual(0, cache.Count);
                TestAssert.IsFalse(cache.IsLoaded);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestDisposeBehaviorAsync()
        {
            TermIdCache cache = new TermIdCache("test_index");
            cache.Set("term", "id");
            cache.Dispose();

            // Operations should throw after dispose
            bool threw = false;
            try
            {
                cache.TryGetId("term", out _);
            }
            catch (ObjectDisposedException)
            {
                threw = true;
            }
            TestAssert.IsTrue(threw);

            threw = false;
            try
            {
                cache.Set("new_term", "new_id");
            }
            catch (ObjectDisposedException)
            {
                threw = true;
            }
            TestAssert.IsTrue(threw);

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestThreadSafetyAsync()
        {
            using (TermIdCache cache = new TermIdCache("test_index"))
            {
                int numThreads = 10;
                int numOperationsPerThread = 1000;
                List<Task> tasks = new List<Task>();
                int completedOperations = 0;

                // Launch multiple concurrent readers and writers
                for (int t = 0; t < numThreads; t++)
                {
                    int threadId = t;
                    tasks.Add(Task.Run(() =>
                    {
                        for (int i = 0; i < numOperationsPerThread; i++)
                        {
                            string term = $"term_{threadId}_{i}";
                            string termId = $"id_{threadId}_{i}";

                            // Write
                            cache.Set(term, termId);

                            // Read
                            cache.TryGetId(term, out string? readId);

                            Interlocked.Increment(ref completedOperations);
                        }
                    }));
                }

                await Task.WhenAll(tasks).ConfigureAwait(false);

                // Verify all operations completed without exception
                TestAssert.AreEqual(numThreads * numOperationsPerThread, completedOperations);

                // Cache should have entries (though exact count depends on timing)
                TestAssert.IsTrue(cache.Count > 0);
            }
        }

        private static async Task TestIsLoadedStateAsync()
        {
            using (TermIdCache cache = new TermIdCache("test_index"))
            {
                // Initially not loaded
                TestAssert.IsFalse(cache.IsLoaded);

                // Set doesn't change IsLoaded
                cache.Set("term", "id");
                TestAssert.IsFalse(cache.IsLoaded);

                // Load sets IsLoaded to true
                cache.Load(new Dictionary<string, string> { ["term2"] = "id2" });
                TestAssert.IsTrue(cache.IsLoaded);

                // Clear sets IsLoaded to false
                cache.Clear();
                TestAssert.IsFalse(cache.IsLoaded);
            }

            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
