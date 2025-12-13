namespace Test
{
    using System;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Tests for VerbexConfiguration functionality.
    /// </summary>
    public static class ConfigurationTests
    {
        /// <summary>
        /// Runs all configuration tests.
        /// </summary>
        /// <param name="runner">Test runner to execute tests.</param>
        /// <returns>Task representing the asynchronous operation.</returns>
        public static async Task RunAllAsync(TestRunner runner)
        {
            await runner.RunTestAsync("Default Configuration Values", TestDefaultConfigurationValuesAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Configuration Validation", TestConfigurationValidationAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Configuration Clone", TestConfigurationCloneAsync).ConfigureAwait(false);
            await runner.RunTestAsync("Factory Methods", TestFactoryMethodsAsync).ConfigureAwait(false);
        }

        private static async Task TestDefaultConfigurationValuesAsync()
        {
            VerbexConfiguration config = new VerbexConfiguration();

            TestAssert.AreEqual(StorageMode.InMemory, config.StorageMode);
            TestAssert.AreEqual(100, config.DefaultMaxSearchResults);
            TestAssert.AreEqual(2.0, config.PhraseSearchBonus);
            TestAssert.AreEqual(10.0, config.SigmoidNormalizationDivisor);
            TestAssert.AreEqual(0, config.MinTokenLength);
            TestAssert.AreEqual(0, config.MaxTokenLength);
            TestAssert.IsNull(config.Lemmatizer);
            TestAssert.IsNull(config.StopWordRemover);
            TestAssert.IsNull(config.Tokenizer);

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestConfigurationValidationAsync()
        {
            VerbexConfiguration config = new VerbexConfiguration();

            // Test MinTokenLength validation
            bool threw = false;
            try
            {
                config.MinTokenLength = -1;
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }
            TestAssert.IsTrue(threw);

            // Test MaxTokenLength validation
            threw = false;
            try
            {
                config.MaxTokenLength = -1;
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }
            TestAssert.IsTrue(threw);

            // Test DefaultMaxSearchResults validation
            threw = false;
            try
            {
                config.DefaultMaxSearchResults = 0;
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }
            TestAssert.IsTrue(threw);

            // Test PhraseSearchBonus validation
            threw = false;
            try
            {
                config.PhraseSearchBonus = 0.0;
            }
            catch (ArgumentOutOfRangeException)
            {
                threw = true;
            }
            TestAssert.IsTrue(threw);

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestConfigurationCloneAsync()
        {
            VerbexConfiguration original = new VerbexConfiguration
            {
                StorageMode = StorageMode.OnDisk,
                StorageDirectory = "/test/path",
                MinTokenLength = 3,
                MaxTokenLength = 50,
                DefaultMaxSearchResults = 200
            };

            VerbexConfiguration clone = original.Clone();

            TestAssert.AreEqual(original.StorageMode, clone.StorageMode);
            TestAssert.AreEqual(original.StorageDirectory, clone.StorageDirectory);
            TestAssert.AreEqual(original.MinTokenLength, clone.MinTokenLength);
            TestAssert.AreEqual(original.MaxTokenLength, clone.MaxTokenLength);
            TestAssert.AreEqual(original.DefaultMaxSearchResults, clone.DefaultMaxSearchResults);

            // Verify they are separate objects
            clone.MinTokenLength = 5;
            TestAssert.AreNotEqual(original.MinTokenLength, clone.MinTokenLength);

            await Task.CompletedTask.ConfigureAwait(false);
        }

        private static async Task TestFactoryMethodsAsync()
        {
            // Test CreateInMemory
            VerbexConfiguration inMemory = VerbexConfiguration.CreateInMemory();
            TestAssert.AreEqual(StorageMode.InMemory, inMemory.StorageMode);

            // Test CreateOnDisk with index name
            VerbexConfiguration onDisk = VerbexConfiguration.CreateOnDisk("testindex");
            TestAssert.AreEqual(StorageMode.OnDisk, onDisk.StorageMode);
            TestAssert.IsNotNull(onDisk.StorageDirectory);

            // Test CreateOnDisk with custom path
            VerbexConfiguration customPath = VerbexConfiguration.CreateOnDisk("/custom/path", "custom.db");
            TestAssert.AreEqual(StorageMode.OnDisk, customPath.StorageMode);
            TestAssert.AreEqual("/custom/path", customPath.StorageDirectory);
            TestAssert.AreEqual("custom.db", customPath.DatabaseFilename);

            await Task.CompletedTask.ConfigureAwait(false);
        }
    }
}
