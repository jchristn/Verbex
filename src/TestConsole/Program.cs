namespace TestConsole
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Interactive test console for exploring Verbex InvertedIndex functionality
    /// Provides a comprehensive shell for testing all aspects of the index
    /// </summary>
    public static class Program
    {
        private static readonly CancellationTokenSource _CancellationTokenSource = new CancellationTokenSource();
        private static volatile bool _ExitRequested = false;

        /// <summary>
        /// Main entry point for the TestConsole application
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Task representing the asynchronous operation</returns>
        public static async Task Main(string[] args)
        {
            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("\nExiting...");
                _ExitRequested = true;
                _CancellationTokenSource.Cancel();
                e.Cancel = false;
            };

            IndexManager indexManager = new IndexManager();
            CommandProcessor commandProcessor = new CommandProcessor(indexManager);

            try
            {
                indexManager.InitializeDefaultConfigurations();
                await indexManager.DiscoverExistingIndicesAsync().ConfigureAwait(false);
                await RunInteractiveShellAsync(indexManager, commandProcessor).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("\nOperation cancelled by user.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nUnexpected error: {ex.Message}");
            }
            finally
            {
                try
                {
                    await indexManager.CleanupAsync().ConfigureAwait(false);
                    Console.WriteLine("Cleanup completed.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error during cleanup: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Runs the interactive shell
        /// </summary>
        /// <param name="indexManager">Index manager</param>
        /// <param name="commandProcessor">Command processor</param>
        /// <returns>Task representing the asynchronous operation</returns>
        private static async Task RunInteractiveShellAsync(IndexManager indexManager, CommandProcessor commandProcessor)
        {
            Console.WriteLine("=== Verbex Interactive Test Console ===");
            Console.WriteLine("Type 'help' for available commands, 'exit' to quit.\n");

            // Initialize default index
            await indexManager.SwitchToIndexAsync("memory").ConfigureAwait(false);

            while (!_ExitRequested)
            {
                try
                {
                    Console.Write($"Verbex[{indexManager.CurrentStorageMode}]> ");
                    string? input = Console.ReadLine();

                    if (string.IsNullOrWhiteSpace(input))
                        continue;

                    string trimmedInput = input.Trim().ToLowerInvariant();

                    // Handle exit commands directly
                    if (trimmedInput == "exit" || trimmedInput == "quit" || trimmedInput == "q")
                    {
                        _ExitRequested = true;
                        break;
                    }

                    await commandProcessor.ProcessCommandAsync(input, _CancellationTokenSource.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error: {ex.Message}");
                }

                Console.WriteLine();
            }
        }
    }
}
