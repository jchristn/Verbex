namespace VerbexCli
{
    using System;
    using System.CommandLine;
    using System.CommandLine.Builder;
    using System.CommandLine.Invocation;
    using System.CommandLine.Parsing;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using VerbexCli.Commands;
    using VerbexCli.Infrastructure;

    /// <summary>
    /// Main entry point for the Verbex CLI application
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point for the application
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Exit code</returns>
        public static async Task<int> Main(string[] args)
        {
            try
            {
                // Create the root command
                RootCommand rootCommand = CreateRootCommand();

                // Build the command line parser with enhanced features
                CommandLineBuilder builder = new CommandLineBuilder(rootCommand)
                    .UseDefaults()
                    .UseExceptionHandler(HandleException)
                    .UseHelp()
                    .EnablePosixBundling()
                    .EnableDirectives()
                    .AddMiddleware(ProcessGlobalOptions);

                Parser parser = builder.Build();

                // Execute the command
                return await parser.InvokeAsync(args).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Unexpected error: {ex.Message}");
                if (Environment.GetEnvironmentVariable("VBX_DEBUG") == "1")
                {
                    OutputManager.WriteError(ex.StackTrace ?? "No stack trace available");
                }
                return 1;
            }
        }

        /// <summary>
        /// Creates the root command with all subcommands
        /// </summary>
        /// <returns>Configured root command</returns>
        private static RootCommand CreateRootCommand()
        {
            RootCommand rootCommand = new RootCommand("Verbex CLI - Professional command-line interface for the Verbex inverted index library")
            {
                Name = "vbx"
            };

            // Add global options
            AddGlobalOptions(rootCommand);

            // Add main command groups
            rootCommand.AddCommand(IndexCommands.CreateIndexCommand());
            rootCommand.AddCommand(DocumentCommands.CreateDocumentCommand());
            rootCommand.AddCommand(SearchCommands.CreateSearchCommand());
            rootCommand.AddCommand(StatsCommands.CreateStatsCommand());
            rootCommand.AddCommand(MaintenanceCommands.CreateMaintenanceCommand());
            rootCommand.AddCommand(ConfigCommands.CreateConfigCommand());

            return rootCommand;
        }

        /// <summary>
        /// Adds global options that apply to all commands
        /// </summary>
        /// <param name="rootCommand">Root command to add options to</param>
        private static void AddGlobalOptions(RootCommand rootCommand)
        {
            // Output format option
            Option<OutputFormat> outputOption = new Option<OutputFormat>(
                aliases: new[] { "--output", "-o" },
                description: "Output format")
            {
                IsRequired = false
            };
            outputOption.SetDefaultValue(OutputFormat.Table);

            // No color option
            Option<bool> noColorOption = new Option<bool>(
                aliases: new[] { "--no-color" },
                description: "Disable colored output")
            {
                IsRequired = false
            };

            // Verbose option
            Option<bool> verboseOption = new Option<bool>(
                aliases: new[] { "--verbose", "-v" },
                description: "Enable verbose output")
            {
                IsRequired = false
            };

            // Quiet option
            Option<bool> quietOption = new Option<bool>(
                aliases: new[] { "--quiet", "-q" },
                description: "Enable quiet output (minimal output)")
            {
                IsRequired = false
            };

            // Debug option
            Option<bool> debugOption = new Option<bool>(
                aliases: new[] { "--debug" },
                description: "Enable debug output")
            {
                IsRequired = false
            };

            // Config directory option
            Option<string?> configDirOption = new Option<string?>(
                aliases: new[] { "--config-dir" },
                description: "Specify custom directory for configuration and index data (default: ~/.vbx). Use 'default' to reset to default directory.")
            {
                IsRequired = false
            };

            rootCommand.AddGlobalOption(outputOption);
            rootCommand.AddGlobalOption(noColorOption);
            rootCommand.AddGlobalOption(verboseOption);
            rootCommand.AddGlobalOption(quietOption);
            rootCommand.AddGlobalOption(debugOption);
            rootCommand.AddGlobalOption(configDirOption);
        }

        /// <summary>
        /// Processes global options and sets them in the OutputManager
        /// </summary>
        /// <param name="context">Invocation context</param>
        /// <param name="next">Next middleware delegate</param>
        /// <returns>Task representing the async operation</returns>
        private static async Task ProcessGlobalOptions(InvocationContext context, Func<InvocationContext, Task> next)
        {
            ParseResult parseResult = context.ParseResult;

            // Store references to the global options for easier access
            Option<OutputFormat>? outputOption = null;
            Option<bool>? noColorOption = null;
            Option<bool>? verboseOption = null;
            Option<bool>? quietOption = null;
            Option<bool>? debugOption = null;
            Option<string?>? configDirOption = null;

            foreach (Option option in parseResult.RootCommandResult.Command.Options)
            {
                if (option is Option<OutputFormat> outOpt && outOpt.HasAlias("--output"))
                    outputOption = outOpt;
                else if (option is Option<bool> boolOpt)
                {
                    if (boolOpt.HasAlias("--no-color"))
                        noColorOption = boolOpt;
                    else if (boolOpt.HasAlias("--verbose"))
                        verboseOption = boolOpt;
                    else if (boolOpt.HasAlias("--quiet"))
                        quietOption = boolOpt;
                    else if (boolOpt.HasAlias("--debug"))
                        debugOption = boolOpt;
                }
                else if (option is Option<string?> strOpt && strOpt.HasAlias("--config-dir"))
                    configDirOption = strOpt;
            }

            // Process output format option
            if (outputOption != null)
            {
                OutputFormat format = parseResult.GetValueForOption(outputOption);
                OutputManager.DefaultFormat = format;
            }

            // Process no-color option
            if (noColorOption != null)
            {
                bool noColor = parseResult.GetValueForOption(noColorOption);
                if (noColor)
                    OutputManager.ColorEnabled = false;
            }

            // Process verbose option
            if (verboseOption != null)
            {
                bool verbose = parseResult.GetValueForOption(verboseOption);
                OutputManager.VerboseEnabled = verbose;
            }

            // Process quiet option
            if (quietOption != null)
            {
                bool quiet = parseResult.GetValueForOption(quietOption);
                OutputManager.QuietEnabled = quiet;
            }

            // Process debug option
            if (debugOption != null)
            {
                bool debug = parseResult.GetValueForOption(debugOption);
                if (debug)
                    Environment.SetEnvironmentVariable("VBX_DEBUG", "1");
            }

            // Process config directory option
            if (configDirOption != null)
            {
                string? configDir = parseResult.GetValueForOption(configDirOption);
                if (!string.IsNullOrEmpty(configDir))
                {
                    if (configDir.Equals("default", StringComparison.OrdinalIgnoreCase))
                    {
                        // Clear the custom config directory setting
                        GlobalConfig.SetConfigDirectory(null);
                        // Don't need to initialize IndexManager - it will use default
                    }
                    else
                    {
                        if (!Path.IsPathRooted(configDir))
                        {
                            configDir = Path.GetFullPath(configDir);
                        }

                        // Save the config directory preference for future commands
                        GlobalConfig.SetConfigDirectory(configDir);

                        // Initialize with the custom directory for this command
                        IndexManager.Initialize(configDir);
                    }
                }
            }

            await next(context).ConfigureAwait(false);
        }

        /// <summary>
        /// Handles unhandled exceptions
        /// </summary>
        /// <param name="exception">Exception that occurred</param>
        /// <param name="context">Parser context</param>
        private static void HandleException(Exception exception, InvocationContext context)
        {
            OutputManager.WriteError($"Error: {exception.Message}");

            // Check for debug flag in arguments or environment
            if (context.ParseResult.Tokens.Any(t => t.Value == "--debug") ||
                Environment.GetEnvironmentVariable("VBX_DEBUG") == "1")
            {
                OutputManager.WriteError($"Stack trace: {exception.StackTrace}");
            }

            context.ExitCode = 1;
        }
    }
}
