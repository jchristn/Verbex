namespace Test.Shared
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Abstraction used by the shared test suites to register test cases.
    /// Implementations decide whether to execute a case immediately or capture it
    /// as a deferred descriptor for a downstream runner (CLI, xUnit, or NUnit).
    /// </summary>
    public interface ITestCollector
    {
        /// <summary>
        /// Registers an asynchronous test case.
        /// </summary>
        /// <param name="testName">Human-readable name of the test case.</param>
        /// <param name="testAction">Delegate that performs the test.</param>
        /// <returns>Task representing the registration operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="testName"/> or <paramref name="testAction"/> is null.</exception>
        Task RunTestAsync(string testName, Func<Task> testAction);

        /// <summary>
        /// Registers a synchronous test case.
        /// </summary>
        /// <param name="testName">Human-readable name of the test case.</param>
        /// <param name="testAction">Delegate that performs the test.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="testName"/> or <paramref name="testAction"/> is null.</exception>
        void RunTest(string testName, Action testAction);
    }
}
