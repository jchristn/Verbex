namespace Test.Automated
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    using Touchstone.Cli;

    using Test.Shared;

    /// <summary>
    /// Command-line runner that executes the shared Verbex test suite through the
    /// Touchstone console runner. This is the automated equivalent of the previous
    /// bespoke console test harness.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">
        /// Optional arguments. Provide <c>--results &lt;path&gt;</c> to export a JSON results file.
        /// </param>
        /// <returns>Exit code: 0 when all tests pass, 1 when any test fails.</returns>
        public static async Task<int> Main(string[] args)
        {
            string? resultsPath = null;

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--results", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
                {
                    resultsPath = args[i + 1];
                    i++;
                }
            }

            Console.WriteLine("Verbex Test Suite (Touchstone CLI runner)");
            Console.WriteLine("=========================================");
            Console.WriteLine();

            return await ConsoleRunner.RunAsync(
                VerbexTestSuites.All,
                sink: null,
                resultsPath: resultsPath,
                cancellationToken: CancellationToken.None).ConfigureAwait(false);
        }
    }
}
