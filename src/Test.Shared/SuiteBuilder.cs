namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading.Tasks;

    using Touchstone.Core;

    using Verbex;

    /// <summary>
    /// Collects test-case registrations from the shared suites and materializes them
    /// as Touchstone <see cref="TestCaseDescriptor"/> instances for deferred execution.
    /// </summary>
    /// <remarks>
    /// The shared suites were originally written against an eager, console-based runner
    /// that executed each case as it was registered. This builder adapts that same
    /// registration surface (<see cref="ITestCollector"/>) into deferred descriptors so
    /// the identical test bodies can be driven by the CLI runner, xUnit, or NUnit.
    /// When a storage mode is supplied, each captured case sets the ambient
    /// <see cref="TestContext"/> storage mode immediately before executing, preserving the
    /// original behavior of running every suite against both in-memory and on-disk storage.
    /// Because execution is deferred, callers must ensure test cases are executed
    /// sequentially (the adapters disable test parallelization for this reason).
    /// </remarks>
    public sealed class SuiteBuilder : ITestCollector
    {
        private readonly string _SuiteId;
        private readonly StorageMode? _StorageMode;
        private readonly List<TestCaseDescriptor> _Cases;
        private int _Index;

        /// <summary>
        /// Initializes a new instance of the <see cref="SuiteBuilder"/> class.
        /// </summary>
        /// <param name="suiteId">Unique identifier for the suite being built.</param>
        /// <param name="storageMode">
        /// Optional storage mode applied to <see cref="TestContext"/> before each case runs.
        /// Pass null for suites that manage their own storage configuration.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="suiteId"/> is null.</exception>
        public SuiteBuilder(string suiteId, StorageMode? storageMode = null)
        {
            _SuiteId = suiteId ?? throw new ArgumentNullException(nameof(suiteId));
            _StorageMode = storageMode;
            _Cases = new List<TestCaseDescriptor>();
            _Index = 0;
        }

        /// <summary>
        /// Gets the descriptors captured so far.
        /// </summary>
        public IReadOnlyList<TestCaseDescriptor> Cases
        {
            get { return _Cases; }
        }

        /// <inheritdoc />
        public Task RunTestAsync(string testName, Func<Task> testAction)
        {
            ArgumentNullException.ThrowIfNull(testName);
            ArgumentNullException.ThrowIfNull(testAction);

            Capture(testName, testAction);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public void RunTest(string testName, Action testAction)
        {
            ArgumentNullException.ThrowIfNull(testName);
            ArgumentNullException.ThrowIfNull(testAction);

            Capture(testName, () =>
            {
                testAction();
                return Task.CompletedTask;
            });
        }

        private void Capture(string testName, Func<Task> testAction)
        {
            StorageMode? mode = _StorageMode;
            string caseId = string.Format("{0:D3}_{1}", _Index, Sanitize(testName));
            _Index++;

            _Cases.Add(new TestCaseDescriptor(
                suiteId: _SuiteId,
                caseId: caseId,
                displayName: testName,
                executeAsync: async cancellationToken =>
                {
                    if (mode.HasValue)
                    {
                        TestContext.SetStorageMode(mode.Value);
                    }

                    await testAction().ConfigureAwait(false);
                }));
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
                else if (c == ' ' || c == '-' || c == '_')
                {
                    sb.Append('_');
                }
            }

            return sb.Length > 0 ? sb.ToString() : "case";
        }
    }
}
