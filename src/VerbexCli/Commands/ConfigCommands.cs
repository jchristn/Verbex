namespace VerbexCli.Commands
{
    using System;
    using System.CommandLine;
    using System.IO;
    using System.Threading.Tasks;
    using VerbexCli.Infrastructure;

    /// <summary>
    /// Commands for managing CLI configuration
    /// </summary>
    public static class ConfigCommands
    {
        /// <summary>
        /// Creates the config command group
        /// </summary>
        /// <returns>Config command</returns>
        public static Command CreateConfigCommand()
        {
            Command configCommand = new Command("config", "Manage CLI configuration");

            // Add subcommands
            configCommand.AddCommand(CreateConfigShowCommand());
            configCommand.AddCommand(CreateConfigSetCommand());
            configCommand.AddCommand(CreateConfigUnsetCommand());

            return configCommand;
        }

        /// <summary>
        /// Creates the config show command
        /// </summary>
        /// <returns>Config show command</returns>
        private static Command CreateConfigShowCommand()
        {
            Command showCommand = new Command("show", "Show current configuration");

            showCommand.SetHandler(async () =>
            {
                await HandleConfigShowAsync().ConfigureAwait(false);
            });

            return showCommand;
        }

        /// <summary>
        /// Creates the config set command
        /// </summary>
        /// <returns>Config set command</returns>
        private static Command CreateConfigSetCommand()
        {
            Command setCommand = new Command("set", "Set a configuration value");

            Argument<string> keyArgument = new Argument<string>("key", "Configuration key");
            Argument<string> valueArgument = new Argument<string>("value", "Configuration value");

            setCommand.AddArgument(keyArgument);
            setCommand.AddArgument(valueArgument);

            setCommand.SetHandler(async (string key, string value) =>
            {
                await HandleConfigSetAsync(key, value).ConfigureAwait(false);
            }, keyArgument, valueArgument);

            return setCommand;
        }

        /// <summary>
        /// Creates the config unset command
        /// </summary>
        /// <returns>Config unset command</returns>
        private static Command CreateConfigUnsetCommand()
        {
            Command unsetCommand = new Command("unset", "Unset a configuration value");

            Argument<string> keyArgument = new Argument<string>("key", "Configuration key");

            unsetCommand.AddArgument(keyArgument);

            unsetCommand.SetHandler(async (string key) =>
            {
                await HandleConfigUnsetAsync(key).ConfigureAwait(false);
            }, keyArgument);

            return unsetCommand;
        }

        // Command handlers

        /// <summary>
        /// Handles the config show command
        /// </summary>
        private static Task HandleConfigShowAsync()
        {
            try
            {
                OutputManager.WriteVerbose("Showing current configuration");

                var indexManager = IndexManager.Instance;

                string effectiveConfigDir = GlobalConfig.GetEffectiveConfigDirectory();
                string? customConfigDir = GlobalConfig.GetConfigDirectory();
                bool isCustomDirectory = !string.IsNullOrEmpty(customConfigDir);

                var config = new
                {
                    CurrentIndex = indexManager.CurrentIndexName ?? "none",
                    AvailableIndices = indexManager.Configurations.Count,
                    DefaultOutputFormat = OutputManager.DefaultFormat.ToString().ToLowerInvariant(),
                    ColorEnabled = OutputManager.ColorEnabled,
                    VerboseEnabled = OutputManager.VerboseEnabled,
                    QuietEnabled = OutputManager.QuietEnabled,
                    ConfigDirectory = effectiveConfigDir,
                    IsCustomConfigDirectory = isCustomDirectory
                };

                OutputManager.WriteData(config);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to show configuration: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the config set command
        /// </summary>
        private static Task HandleConfigSetAsync(string key, string value)
        {
            try
            {
                OutputManager.WriteVerbose($"Setting configuration '{key}' = '{value}'");

                switch (key.ToLowerInvariant())
                {
                    case "output":
                    case "outputformat":
                        if (Enum.TryParse<OutputFormat>(value, true, out OutputFormat format))
                        {
                            OutputManager.DefaultFormat = format;
                            OutputManager.WriteSuccess($"Output format set to '{format}'");
                        }
                        else
                        {
                            throw new ArgumentException($"Invalid output format: {value}. Valid values: table, json, csv, yaml");
                        }
                        break;

                    case "color":
                        if (bool.TryParse(value, out bool colorEnabled))
                        {
                            OutputManager.ColorEnabled = colorEnabled;
                            OutputManager.WriteSuccess($"Color output {(colorEnabled ? "enabled" : "disabled")}");
                        }
                        else
                        {
                            throw new ArgumentException($"Invalid boolean value: {value}");
                        }
                        break;

                    case "verbose":
                        if (bool.TryParse(value, out bool verboseEnabled))
                        {
                            OutputManager.VerboseEnabled = verboseEnabled;
                            OutputManager.WriteSuccess($"Verbose output {(verboseEnabled ? "enabled" : "disabled")}");
                        }
                        else
                        {
                            throw new ArgumentException($"Invalid boolean value: {value}");
                        }
                        break;

                    default:
                        throw new ArgumentException($"Unknown configuration key: {key}");
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to set configuration '{key}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the config unset command
        /// </summary>
        private static Task HandleConfigUnsetAsync(string key)
        {
            try
            {
                OutputManager.WriteVerbose($"Unsetting configuration '{key}'");

                switch (key.ToLowerInvariant())
                {
                    case "output":
                    case "outputformat":
                        OutputManager.DefaultFormat = OutputFormat.Table;
                        OutputManager.WriteSuccess("Output format reset to default (table)");
                        break;

                    case "color":
                        OutputManager.ColorEnabled = true;
                        OutputManager.WriteSuccess("Color output reset to default (enabled)");
                        break;

                    case "verbose":
                        OutputManager.VerboseEnabled = false;
                        OutputManager.WriteSuccess("Verbose output reset to default (disabled)");
                        break;

                    default:
                        throw new ArgumentException($"Unknown configuration key: {key}");
                }
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to unset configuration '{key}': {ex.Message}");
                throw;
            }
        }
    }
}