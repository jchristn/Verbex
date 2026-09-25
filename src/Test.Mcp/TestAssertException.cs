namespace Test.Mcp
{
    using System;

    /// <summary>
    /// Raised by the test harness when an assertion fails.
    /// </summary>
    public class TestAssertException : Exception
    {
        /// <summary>
        /// Instantiate the exception.
        /// </summary>
        /// <param name="message">Assertion failure message.</param>
        public TestAssertException(string message) : base(message)
        {
        }
    }
}
