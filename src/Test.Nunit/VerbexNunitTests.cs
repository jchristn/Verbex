namespace Test.Nunit
{
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;

    using NUnit.Framework;

    using Touchstone.Core;
    using Touchstone.NunitAdapter;

    using Test.Shared;

    /// <summary>
    /// NUnit entry point for the shared Verbex test suite. Every Touchstone test case is
    /// projected into an individual NUnit test case so failures are reported per case.
    /// </summary>
    [TestFixture]
    public sealed class VerbexNunitTests
    {
        /// <summary>
        /// Gets the shared test cases as an NUnit test-case source (one entry per non-skipped case).
        /// </summary>
        public static IEnumerable Cases
        {
            get { return new TouchstoneTestCaseSource(VerbexTestSuites.All); }
        }

        /// <summary>
        /// Executes a single shared test case.
        /// </summary>
        /// <param name="testCase">The test case to execute.</param>
        /// <returns>Task representing the asynchronous test execution.</returns>
        [Test]
        [TestCaseSource(nameof(Cases))]
        public async Task Run(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }
}
