namespace Test
{
    using System;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Test application demonstrating the Verbex inverted index functionality.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point for the test application.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task Main(string[] args)
        {
            Console.WriteLine("Verbex Comprehensive Test Suite");
            Console.WriteLine("===============================");

            try
            {
                await RunComprehensiveTestSuiteAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Runs the comprehensive test suite covering all aspects of the inverted index.
        /// </summary>
        /// <returns>Task representing the asynchronous operation.</returns>
        private static async Task RunComprehensiveTestSuiteAsync()
        {
            Console.WriteLine("Starting Verbex Comprehensive Test Suite...");
            Console.WriteLine("Testing all functionality across both storage modes...");
            Console.WriteLine();

            TestRunner runner = new TestRunner();

            // Test each storage mode with all test suites
            await RunTestsForStorageMode(runner, "IN-MEMORY", StorageMode.InMemory).ConfigureAwait(false);
            await RunTestsForStorageMode(runner, "ON-DISK", StorageMode.OnDisk).ConfigureAwait(false);

            Console.WriteLine();
            Console.WriteLine("=== STORAGE MODE SPECIFIC TESTS ===");
            await StorageModeTests.RunAllAsync(runner).ConfigureAwait(false);

            // Print summary
            runner.PrintSummary();

            Console.WriteLine();
            Console.WriteLine("Comprehensive test suite completed!");
        }

        /// <summary>
        /// Runs all test suites for a specific storage mode.
        /// </summary>
        /// <param name="runner">Test runner.</param>
        /// <param name="modeName">Name of the storage mode for display.</param>
        /// <param name="storageMode">Storage mode to test.</param>
        /// <returns>Task representing the operation.</returns>
        private static async Task RunTestsForStorageMode(TestRunner runner, string modeName, StorageMode storageMode)
        {
            Console.WriteLine($"=== {modeName} MODE TESTS ===");

            // Set up test context for this storage mode
            TestContext.SetStorageMode(storageMode);

            try
            {
                Console.WriteLine($"--- Basic Tests ({modeName}) ---");
                await InvertedIndexBasicTests.RunAllAsync(runner).ConfigureAwait(false);

                Console.WriteLine($"--- IDisposable Tests ({modeName}) ---");
                await InvertedIndexDisposableTests.RunAllAsync(runner).ConfigureAwait(false);

                Console.WriteLine($"--- Statistics Tests ({modeName}) ---");
                await InvertedIndexStatisticsTests.RunAllAsync(runner).ConfigureAwait(false);

                Console.WriteLine($"--- Configuration Tests ({modeName}) ---");
                await ConfigurationTests.RunAllAsync(runner).ConfigureAwait(false);

                Console.WriteLine($"--- Text Processing Tests ({modeName}) ---");
                await TextProcessingTests.RunAllAsync(runner).ConfigureAwait(false);

                Console.WriteLine($"--- Document Metadata Retrieval Tests ({modeName}) ---");
                await DocumentMetadataRetrievalTests.RunAllAsync(runner).ConfigureAwait(false);

                Console.WriteLine($"--- Metadata Filter Tests ({modeName}) ---");
                await MetadataFilterTests.RunAllAsync(runner).ConfigureAwait(false);

                Console.WriteLine($"--- Labels and Tags Tests ({modeName}) ---");
                await LabelsAndTagsTests.RunAllAsync(runner).ConfigureAwait(false);

                Console.WriteLine($"--- Labels and Tags Update Tests ({modeName}) ---");
                await LabelsAndTagsUpdateTests.RunAllAsync(runner).ConfigureAwait(false);

                Console.WriteLine($"--- Search Filter Tests ({modeName}) ---");
                await SearchFilterTests.RunAllAsync(runner).ConfigureAwait(false);

                Console.WriteLine();
            }
            finally
            {
                // Clean up test context
                TestContext.ClearStorageMode();
            }
        }
    }
}
