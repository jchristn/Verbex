namespace Test
{
    using System;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for text processing functionality including lemmatization,
    /// stop word removal, and token length filtering.
    /// </summary>
    public static class TextProcessingTests
    {
        /// <summary>
        /// Runs all text processing tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("BasicLemmatizer Functionality Test", TestBasicLemmatizerFunctionalityAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Lemmatizer Integration with Index Test", TestLemmatizerIntegrationAsync).ConfigureAwait(false);
            await runner.RunTestAsync("BasicStopWordRemover Functionality Test", TestBasicStopWordRemoverFunctionalityAsync).ConfigureAwait(false);
            await runner.RunTestAsync("StopWordRemover Integration with Index Test", TestStopWordRemoverIntegrationAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Token Length Filtering Test", TestTokenLengthFilteringAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Combined Text Processing Test", TestCombinedTextProcessingAsync).ConfigureAwait(false);
        }

        private static async Task TestBasicLemmatizerFunctionalityAsync()
        {
            BasicLemmatizer lemmatizer = new BasicLemmatizer();

            // Test irregular verbs
            TestAssert.AreEqual("be", lemmatizer.Lemmatize("was"));
            TestAssert.AreEqual("be", lemmatizer.Lemmatize("were"));
            TestAssert.AreEqual("have", lemmatizer.Lemmatize("had"));
            TestAssert.AreEqual("go", lemmatizer.Lemmatize("went"));

            // Test irregular nouns
            TestAssert.AreEqual("child", lemmatizer.Lemmatize("children"));
            TestAssert.AreEqual("mouse", lemmatizer.Lemmatize("mice"));

            // Test regular suffix rules
            TestAssert.AreEqual("cat", lemmatizer.Lemmatize("cats"));
            TestAssert.AreEqual("run", lemmatizer.Lemmatize("running"));
            TestAssert.AreEqual("walk", lemmatizer.Lemmatize("walked"));

            // Test case insensitivity
            TestAssert.AreEqual("be", lemmatizer.Lemmatize("WAS"));
            TestAssert.AreEqual("cat", lemmatizer.Lemmatize("CATS"));

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestLemmatizerIntegrationAsync()
        {
            VerbexConfiguration config = new VerbexConfiguration
            {
                Lemmatizer = new BasicLemmatizer()
            };

            await using InvertedIndex index = await TestContext.CreateTestIndexAsync(customConfig: config).ConfigureAwait(false);

            await index.AddDocumentAsync("test.txt", "cats running walked children were going").ConfigureAwait(false);

            // Search should find lemmatized forms
            SearchResults results;

            results = await index.SearchAsync("cat").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            results = await index.SearchAsync("run").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            results = await index.SearchAsync("walk").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            results = await index.SearchAsync("child").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            results = await index.SearchAsync("be").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            results = await index.SearchAsync("go").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);
        }

        private static async Task TestBasicStopWordRemoverFunctionalityAsync()
        {
            BasicStopWordRemover stopWordRemover = new BasicStopWordRemover();

            // Test common stop words
            TestAssert.IsTrue(stopWordRemover.IsStopWord("the"));
            TestAssert.IsTrue(stopWordRemover.IsStopWord("and"));
            TestAssert.IsTrue(stopWordRemover.IsStopWord("or"));
            TestAssert.IsTrue(stopWordRemover.IsStopWord("in"));
            TestAssert.IsTrue(stopWordRemover.IsStopWord("on"));

            // Test non-stop words
            TestAssert.IsFalse(stopWordRemover.IsStopWord("cat"));
            TestAssert.IsFalse(stopWordRemover.IsStopWord("running"));
            TestAssert.IsFalse(stopWordRemover.IsStopWord("computer"));

            // Test case insensitivity
            TestAssert.IsTrue(stopWordRemover.IsStopWord("THE"));
            TestAssert.IsTrue(stopWordRemover.IsStopWord("And"));

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestStopWordRemoverIntegrationAsync()
        {
            VerbexConfiguration config = new VerbexConfiguration
            {
                StopWordRemover = new BasicStopWordRemover()
            };

            await using InvertedIndex index = await TestContext.CreateTestIndexAsync(customConfig: config).ConfigureAwait(false);

            await index.AddDocumentAsync("test.txt", "the cat and the dog are running in the garden").ConfigureAwait(false);

            // Content words should be searchable
            SearchResults results;

            results = await index.SearchAsync("cat").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            results = await index.SearchAsync("dog").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            results = await index.SearchAsync("garden").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            // Stop words should not be searchable
            results = await index.SearchAsync("the").ConfigureAwait(false);
            TestAssert.AreEqual(0, results.TotalCount);

            results = await index.SearchAsync("and").ConfigureAwait(false);
            TestAssert.AreEqual(0, results.TotalCount);
        }

        private static async Task TestTokenLengthFilteringAsync()
        {
            VerbexConfiguration config = new VerbexConfiguration
            {
                MinTokenLength = 3,
                MaxTokenLength = 10
            };

            await using InvertedIndex index = await TestContext.CreateTestIndexAsync(customConfig: config).ConfigureAwait(false);

            await index.AddDocumentAsync("test.txt", "a cat dog elephant supercalifragilisticexpialidocious").ConfigureAwait(false);

            // Tokens within length range should be searchable
            SearchResults results;

            results = await index.SearchAsync("cat").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            results = await index.SearchAsync("dog").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            results = await index.SearchAsync("elephant").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            // Tokens outside length range should not be searchable
            results = await index.SearchAsync("a").ConfigureAwait(false); // Too short
            TestAssert.AreEqual(0, results.TotalCount);

            results = await index.SearchAsync("supercalifragilisticexpialidocious").ConfigureAwait(false); // Too long
            TestAssert.AreEqual(0, results.TotalCount);
        }

        private static async Task TestCombinedTextProcessingAsync()
        {
            VerbexConfiguration config = new VerbexConfiguration
            {
                MinTokenLength = 3,
                MaxTokenLength = 15,
                Lemmatizer = new BasicLemmatizer(),
                StopWordRemover = new BasicStopWordRemover()
            };

            await using InvertedIndex index = await TestContext.CreateTestIndexAsync(customConfig: config).ConfigureAwait(false);

            await index.AddDocumentAsync("test.txt", "The cats were running quickly in the beautiful gardens").ConfigureAwait(false);

            SearchResults results;

            // Should find lemmatized forms
            results = await index.SearchAsync("cat").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            results = await index.SearchAsync("run").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            // Should find regular content words
            results = await index.SearchAsync("quickly").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            results = await index.SearchAsync("beautiful").ConfigureAwait(false);
            TestAssert.AreEqual(1, results.TotalCount);

            // Stop words should not be found
            results = await index.SearchAsync("the").ConfigureAwait(false);
            TestAssert.AreEqual(0, results.TotalCount);

            results = await index.SearchAsync("were").ConfigureAwait(false);
            TestAssert.AreEqual(0, results.TotalCount);
        }
    }
}
