namespace Test.Xunit
{
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Core;
    using Touchstone.XunitAdapter;

    using Test.Shared;

    using global::Xunit;

    /// <summary>
    /// xUnit entry point for the shared Verbex test suite. Every Touchstone test case is
    /// projected into an individual xUnit theory row so failures are reported per case.
    /// </summary>
    public sealed class VerbexXunitTests
    {
        /// <summary>
        /// Gets the shared test cases as xUnit theory data (one row per non-skipped case).
        /// </summary>
        public static TouchstoneTheoryData Cases
        {
            get { return new TouchstoneTheoryData(VerbexTestSuites.All); }
        }

        /// <summary>
        /// Executes a single shared test case.
        /// </summary>
        /// <param name="testCase">The test case to execute.</param>
        /// <returns>Task representing the asynchronous test execution.</returns>
        [Theory]
        [MemberData(nameof(Cases))]
        public async Task Run(TestCaseDescriptor testCase)
        {
            await testCase.ExecuteAsync(CancellationToken.None);
        }
    }
}
