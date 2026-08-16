namespace VerbexCli
{
    using System;
    using System.CommandLine;
    using System.IO;
    using System.Threading.Tasks;
    using VerbexCli.Commands;
    using VerbexCli.Infrastructure;

    /// <summary>
    /// Main entry point for the Verbex CLI application
    /// </summary>
    public static class Program
    {
        private static Option<OutputFormat> _OutputOption = null!;
        private static Option<bool> _NoColorOption = null!;
        private static Option<bool> _VerboseOption = null!;
        private static Option<bool> _QuietOption = null!;
        private static Option<bool> _DebugOption = null!;
        private static Option<string?> _ConfigDirOption = null!;

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

                // Parse and process global options before invoking the matched command
                ParseResult parseResult = rootCommand.Parse(args);
                ProcessGlobalOptions(parseResult);

                // Execute the command
                return await parseResult.InvokeAsync().ConfigureAwait(false);
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
            RootCommand rootCommand = new RootCommand("Verbex CLI - Professional command-line interface for the Verbex inverted index library");

            // Add global options
            AddGlobalOptions(rootCommand);

            // Add main command groups
            rootCommand.Subcommands.Add(IndexCommands.CreateIndexCommand());
            rootCommand.Subcommands.Add(DocumentCommands.CreateDocumentCommand());
            rootCommand.Subcommands.Add(SearchCommands.CreateSearchCommand());
            rootCommand.Subcommands.Add(StatsCommands.CreateStatsCommand());
            rootCommand.Subcommands.Add(MaintenanceCommands.CreateMaintenanceCommand());
            rootCommand.Subcommands.Add(ConfigCommands.CreateConfigCommand());
            rootCommand.Subcommands.Add(AdminCommands.CreateAdminCommand());
            rootCommand.Subcommands.Add(BackupCommands.CreateBackupCommand());
            rootCommand.Subcommands.Add(BackupCommands.CreateRestoreCommand());

            return rootCommand;
        }

        /// <summary>
        /// Adds global options that apply to all commands
        /// </summary>
        /// <param name="rootCommand">Root command to add options to</param>
        private static void AddGlobalOptions(RootCommand rootCommand)
        {
            // Output format option
            _OutputOption = new Option<OutputFormat>("--output", "-o")
            {
                Description = "Output format",
                DefaultValueFactory = _ => OutputFormat.Table,
                Recursive = true
            };

            // No color option
            _NoColorOption = new Option<bool>("--no-color")
            {
                Description = "Disable colored output",
                Recursive = true
            };

            // Verbose option
            _VerboseOption = new Option<bool>("--verbose", "-v")
            {
                Description = "Enable verbose output",
                Recursive = true
            };

            // Quiet option
            _QuietOption = new Option<bool>("--quiet", "-q")
            {
                Description = "Enable quiet output (minimal output)",
                Recursive = true
            };

            // Debug option
            _DebugOption = new Option<bool>("--debug")
            {
                Description = "Enable debug output",
                Recursive = true
            };

            // Config directory option
            _ConfigDirOption = new Option<string?>("--config-dir")
            {
                Description = "Specify custom directory for configuration and index data (default: ~/.vbx). Use 'default' to reset to default directory.",
                Recursive = true
            };

            rootCommand.Options.Add(_OutputOption);
            rootCommand.Options.Add(_NoColorOption);
            rootCommand.Options.Add(_VerboseOption);
            rootCommand.Options.Add(_QuietOption);
            rootCommand.Options.Add(_DebugOption);
            rootCommand.Options.Add(_ConfigDirOption);
        }

        /// <summary>
        /// Processes global options and sets them in the OutputManager
        /// </summary>
        /// <param name="parseResult">Parsed command line result</param>
        private static void ProcessGlobalOptions(ParseResult parseResult)
        {
            // Process output format option
            OutputFormat format = parseResult.GetValue(_OutputOption);
            OutputManager.DefaultFormat = format;

            // Process no-color option
            if (parseResult.GetValue(_NoColorOption))
                OutputManager.ColorEnabled = false;

            // Process verbose option
            OutputManager.VerboseEnabled = parseResult.GetValue(_VerboseOption);

            // Process quiet option
            OutputManager.QuietEnabled = parseResult.GetValue(_QuietOption);

            // Process debug option
            if (parseResult.GetValue(_DebugOption))
                Environment.SetEnvironmentVariable("VBX_DEBUG", "1");

            // Process config directory option
            string? configDir = parseResult.GetValue(_ConfigDirOption);
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
    }
}
