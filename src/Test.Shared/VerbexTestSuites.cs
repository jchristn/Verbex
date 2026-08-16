namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    using Touchstone.Core;

    using Verbex;

    /// <summary>
    /// Central source of truth for the Verbex test suite. Exposes every test case as a
    /// runner-agnostic Touchstone <see cref="TestSuiteDescriptor"/> so the identical set of
    /// tests can be executed by the CLI runner (Test.Automated), the xUnit adapter (Test.Xunit),
    /// and the NUnit adapter (Test.Nunit).
    /// </summary>
    /// <remarks>
    /// The core index suites are materialized once per <see cref="StorageMode"/> (in-memory and
    /// on-disk), mirroring the original console runner which exercised every suite against both
    /// storage modes. Storage-mode-agnostic suites (storage mode behavior, multi-tenant database
    /// drivers, and request-history summaries) are materialized once.
    /// </remarks>
    public static class VerbexTestSuites
    {
        private static readonly object _Lock = new object();
        private static IReadOnlyList<TestSuiteDescriptor>? _All;

        /// <summary>
        /// Gets every Verbex test suite. The result is computed once and cached.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                lock (_Lock)
                {
                    if (_All == null)
                    {
                        _All = Build();
                    }

                    return _All;
                }
            }
        }

        private static IReadOnlyList<TestSuiteDescriptor> Build()
        {
            EnsureContextInitialized();

            List<TestSuiteDescriptor> suites = new List<TestSuiteDescriptor>();

            StorageMode[] modes = new[] { StorageMode.InMemory, StorageMode.OnDisk };

            foreach (StorageMode mode in modes)
            {
                string suffix = mode == StorageMode.InMemory ? "InMemory" : "OnDisk";

                AddCoreSuite(suites, "Basic", suffix, mode, InvertedIndexBasicTests.RunAllAsync);
                AddCoreSuite(suites, "IDisposable", suffix, mode, InvertedIndexDisposableTests.RunAllAsync);
                AddCoreSuite(suites, "Statistics", suffix, mode, InvertedIndexStatisticsTests.RunAllAsync);
                AddCoreSuite(suites, "Configuration", suffix, mode, ConfigurationTests.RunAllAsync);
                AddCoreSuite(suites, "TermIdCache", suffix, mode, TermIdCacheTests.RunAllAsync);
                AddCoreSuite(suites, "Text Processing", suffix, mode, TextProcessingTests.RunAllAsync);
                AddCoreSuite(suites, "Document Metadata Retrieval", suffix, mode, DocumentMetadataRetrievalTests.RunAllAsync);
                AddCoreSuite(suites, "Metadata Filter", suffix, mode, MetadataFilterTests.RunAllAsync);
                AddCoreSuite(suites, "Labels and Tags", suffix, mode, LabelsAndTagsTests.RunAllAsync);
                AddCoreSuite(suites, "Labels and Tags Update", suffix, mode, LabelsAndTagsUpdateTests.RunAllAsync);
                AddCoreSuite(suites, "Search Filter", suffix, mode, SearchFilterTests.RunAllAsync);
                AddCoreSuite(suites, "Batch Document Retrieval", suffix, mode, BatchDocumentRetrievalTests.RunAllAsync);
                AddCoreSuite(suites, "Batch Add Documents", suffix, mode, BatchAddDocumentsTests.RunAllAsync);
                AddCoreSuite(suites, "Batch Delete Documents", suffix, mode, BatchDeleteDocumentsTests.RunAllAsync);
                AddCoreSuite(suites, "Batch Existence Check", suffix, mode, BatchExistenceCheckTests.RunAllAsync);
                AddCoreSuite(suites, "Concurrency", suffix, mode, ConcurrencyTests.RunAllAsync);
                AddCoreSuite(suites, "Enumeration", suffix, mode, EnumerationTests.RunAllAsync);
                AddCoreSuite(suites, "Enumeration Filter", suffix, mode, EnumerationFilterTests.RunAllAsync);
                AddCoreSuite(suites, "Wildcard Search", suffix, mode, WildcardSearchTests.RunAllAsync);
                AddCoreSuite(suites, "Search Enrichment", suffix, mode, SearchEnrichmentTests.RunAllAsync);
            }

            AddSuite(suites, "Argument Validation", StorageMode.InMemory, NegativeValidationTests.RunAllAsync);
            AddSuite(suites, "Storage Mode", null, StorageModeTests.RunAllAsync);
            AddSuite(suites, "Database Driver", null, DatabaseDriverTests.RunAllAsync);
            AddSuite(suites, "Request History Service", null, RequestHistoryServiceTests.RunAllAsync);

            return suites;
        }

        private static void AddCoreSuite(
            List<TestSuiteDescriptor> suites,
            string name,
            string suffix,
            StorageMode mode,
            Func<ITestCollector, Task> runAllAsync)
        {
            string displayName = string.Format("{0} ({1})", name, suffix);
            AddSuite(suites, displayName, mode, runAllAsync);
        }

        private static void AddSuite(
            List<TestSuiteDescriptor> suites,
            string displayName,
            StorageMode? mode,
            Func<ITestCollector, Task> runAllAsync)
        {
            string suiteId = Sanitize(displayName);
            SuiteBuilder builder = new SuiteBuilder(suiteId, mode);

            // The shared suites register their cases synchronously through the collector,
            // so this completes without blocking on real asynchronous work.
            runAllAsync(builder).GetAwaiter().GetResult();

            suites.Add(new TestSuiteDescriptor(
                suiteId: suiteId,
                displayName: displayName,
                cases: builder.Cases));
        }

        private static void EnsureContextInitialized()
        {
            if (TestContext.Options == null)
            {
                // Default to SQLite in-memory, matching the console runner's default database.
                TestContext.Initialize(CommandLineOptions.Parse(new[] { "--db", "sqlite-ram" }));
            }
        }

        private static string Sanitize(string value)
        {
            StringBuilder sb = new StringBuilder(value.Length);

            foreach (char c in value)
            {
                if (char.IsLetterOrDigit(c))
                {
                    sb.Append(c);
                }
                else if (c == ' ' || c == '-' || c == '(' || c == ')')
                {
                    sb.Append('_');
                }
            }

            return sb.ToString();
        }
    }
}
