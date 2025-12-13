namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Test runner that executes and tracks test results
    /// </summary>
    public class TestRunner
    {
        private int _PassedTests;
        private int _FailedTests;
        private readonly List<string> _FailureMessages;

        /// <summary>
        /// Initializes a new instance of the TestRunner class
        /// </summary>
        public TestRunner()
        {
            _PassedTests = 0;
            _FailedTests = 0;
            _FailureMessages = new List<string>();
        }

        /// <summary>
        /// Gets the number of tests that passed
        /// </summary>
        public int PassedTests
        {
            get { return _PassedTests; }
        }

        /// <summary>
        /// Gets the number of tests that failed
        /// </summary>
        public int FailedTests
        {
            get { return _FailedTests; }
        }

        /// <summary>
        /// Gets the total number of tests run
        /// </summary>
        public int TotalTests
        {
            get { return _PassedTests + _FailedTests; }
        }

        /// <summary>
        /// Gets a read-only list of failure messages
        /// </summary>
        public IReadOnlyList<string> FailureMessages
        {
            get { return _FailureMessages.AsReadOnly(); }
        }

        /// <summary>
        /// Runs a test and tracks the result
        /// </summary>
        /// <param name="testName">Name of the test</param>
        /// <param name="testAction">Test action to execute</param>
        /// <returns>Task representing the asynchronous operation</returns>
        /// <exception cref="ArgumentNullException">Thrown when testName or testAction is null</exception>
        public async Task RunTestAsync(string testName, Func<Task> testAction)
        {
            ArgumentNullException.ThrowIfNull(testName);
            ArgumentNullException.ThrowIfNull(testAction);

            Console.Write($"Running {testName}... ");

            try
            {
                await testAction().ConfigureAwait(false);
                _PassedTests++;
                Console.WriteLine("PASSED");
            }
            catch (Exception ex)
            {
                _FailedTests++;
                string failureMessage = $"{testName}: {ex.Message}";
                _FailureMessages.Add(failureMessage);
                Console.WriteLine($"FAILED - {ex.Message}");
            }
        }

        /// <summary>
        /// Runs a synchronous test and tracks the result
        /// </summary>
        /// <param name="testName">Name of the test</param>
        /// <param name="testAction">Test action to execute</param>
        /// <exception cref="ArgumentNullException">Thrown when testName or testAction is null</exception>
        public void RunTest(string testName, Action testAction)
        {
            ArgumentNullException.ThrowIfNull(testName);
            ArgumentNullException.ThrowIfNull(testAction);

            Console.Write($"Running {testName}... ");

            try
            {
                testAction();
                _PassedTests++;
                Console.WriteLine("PASSED");
            }
            catch (Exception ex)
            {
                _FailedTests++;
                string failureMessage = $"{testName}: {ex.Message}";
                _FailureMessages.Add(failureMessage);
                Console.WriteLine($"FAILED - {ex.Message}");
            }
        }

        /// <summary>
        /// Prints a summary of test results
        /// </summary>
        public void PrintSummary()
        {
            Console.WriteLine();
            Console.WriteLine("=== TEST SUMMARY ===");
            Console.WriteLine($"Total Tests: {TotalTests}");
            Console.WriteLine($"Passed: {PassedTests}");
            Console.WriteLine($"Failed: {FailedTests}");

            if (_FailedTests > 0)
            {
                Console.WriteLine();
                Console.WriteLine("FAILURES:");
                foreach (string failure in _FailureMessages)
                {
                    Console.WriteLine($"  - {failure}");
                }
            }

            double successRate = TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0;
            Console.WriteLine($"Success Rate: {successRate:F1}%");
        }
    }
}