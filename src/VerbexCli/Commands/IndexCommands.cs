namespace VerbexCli.Commands
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using VerbexCli.Infrastructure;

    /// <summary>
    /// Commands for managing Verbex indices
    /// </summary>
    public static class IndexCommands
    {
        /// <summary>
        /// Creates the index command group
        /// </summary>
        /// <returns>Index command</returns>
        public static Command CreateIndexCommand()
        {
            Command indexCommand = new Command("index", "Manage Verbex indices");

            // Add subcommands
            indexCommand.AddCommand(CreateIndexCreateCommand());
            indexCommand.AddCommand(CreateIndexListCommand());
            indexCommand.AddCommand(CreateIndexUseCommand());
            indexCommand.AddCommand(CreateIndexDeleteCommand());
            indexCommand.AddCommand(CreateIndexInfoCommand());
            indexCommand.AddCommand(CreateIndexExportCommand());

            return indexCommand;
        }

        /// <summary>
        /// Creates the index create command
        /// </summary>
        /// <returns>Index create command</returns>
        private static Command CreateIndexCreateCommand()
        {
            Command createCommand = new Command("create", "Create a new index");

            Argument<string> nameArgument = new Argument<string>("name", "Name of the index to create");

            Option<string> storageOption = new Option<string>(
                aliases: new[] { "--storage", "-s" },
                description: "Storage mode (memory, disk, hybrid)")
            {
                IsRequired = false
            };
            storageOption.SetDefaultValue("memory");

            Option<bool> lemmatizerOption = new Option<bool>(
                aliases: new[] { "--lemmatizer", "-l" },
                description: "Enable lemmatization")
            {
                IsRequired = false
            };

            Option<bool> stopWordsOption = new Option<bool>(
                aliases: new[] { "--stopwords", "-w" },
                description: "Enable stop word removal")
            {
                IsRequired = false
            };

            Option<int> minLengthOption = new Option<int>(
                aliases: new[] { "--min-length" },
                description: "Minimum token length")
            {
                IsRequired = false
            };

            Option<int> maxLengthOption = new Option<int>(
                aliases: new[] { "--max-length" },
                description: "Maximum token length")
            {
                IsRequired = false
            };

            Option<string[]> tagOption = new Option<string[]>(
                aliases: new[] { "--tag", "-t" },
                description: "Tags in key=value format (repeatable)")
            {
                IsRequired = false,
                AllowMultipleArgumentsPerToken = true
            };

            Option<string[]> labelOption = new Option<string[]>(
                aliases: new[] { "--label", "-L" },
                description: "Labels to associate with the index (repeatable)")
            {
                IsRequired = false,
                AllowMultipleArgumentsPerToken = true
            };

            createCommand.AddArgument(nameArgument);
            createCommand.AddOption(storageOption);
            createCommand.AddOption(lemmatizerOption);
            createCommand.AddOption(stopWordsOption);
            createCommand.AddOption(minLengthOption);
            createCommand.AddOption(maxLengthOption);
            createCommand.AddOption(tagOption);
            createCommand.AddOption(labelOption);

            createCommand.SetHandler(async (string name, string storage, bool lemmatizer, bool stopWords, int minLength, int maxLength, string[]? tags, string[]? labels) =>
            {
                await HandleIndexCreateAsync(name, storage, lemmatizer, stopWords, minLength, maxLength, tags, labels).ConfigureAwait(false);
            }, nameArgument, storageOption, lemmatizerOption, stopWordsOption, minLengthOption, maxLengthOption, tagOption, labelOption);

            return createCommand;
        }

        /// <summary>
        /// Creates the index list command
        /// </summary>
        /// <returns>Index list command</returns>
        private static Command CreateIndexListCommand()
        {
            Command listCommand = new Command("ls", "List all available indices");

            listCommand.SetHandler(async () =>
            {
                await HandleIndexListAsync().ConfigureAwait(false);
            });

            return listCommand;
        }

        /// <summary>
        /// Creates the index use command
        /// </summary>
        /// <returns>Index use command</returns>
        private static Command CreateIndexUseCommand()
        {
            Command useCommand = new Command("use", "Switch to a different index");

            Argument<string> nameArgument = new Argument<string>("name", "Name of the index to use");
            useCommand.AddArgument(nameArgument);

            useCommand.SetHandler(async (string name) =>
            {
                await HandleIndexUseAsync(name).ConfigureAwait(false);
            }, nameArgument);

            return useCommand;
        }

        /// <summary>
        /// Creates the index delete command
        /// </summary>
        /// <returns>Index delete command</returns>
        private static Command CreateIndexDeleteCommand()
        {
            Command deleteCommand = new Command("delete", "Delete an index");

            Argument<string> nameArgument = new Argument<string>("name", "Name of the index to delete");

            Option<bool> forceOption = new Option<bool>(
                aliases: new[] { "--force", "-f" },
                description: "Force deletion without confirmation")
            {
                IsRequired = false
            };

            deleteCommand.AddArgument(nameArgument);
            deleteCommand.AddOption(forceOption);

            deleteCommand.SetHandler(async (string name, bool force) =>
            {
                await HandleIndexDeleteAsync(name, force).ConfigureAwait(false);
            }, nameArgument, forceOption);

            return deleteCommand;
        }

        /// <summary>
        /// Creates the index info command
        /// </summary>
        /// <returns>Index info command</returns>
        private static Command CreateIndexInfoCommand()
        {
            Command infoCommand = new Command("info", "Show information about an index");

            Argument<string> nameArgument = new Argument<string>("name", "Name of the index (optional - shows current if not specified)")
            {
                Arity = ArgumentArity.ZeroOrOne
            };

            infoCommand.AddArgument(nameArgument);

            infoCommand.SetHandler(async (string? name) =>
            {
                await HandleIndexInfoAsync(name).ConfigureAwait(false);
            }, nameArgument);

            return infoCommand;
        }

        /// <summary>
        /// Creates the index export command
        /// </summary>
        /// <returns>Index export command</returns>
        private static Command CreateIndexExportCommand()
        {
            Command exportCommand = new Command("export", "Export index data to a file");

            Argument<string> nameArgument = new Argument<string>("name", "Name of the index to export");
            Argument<string> fileArgument = new Argument<string>("file", "Output file path");

            exportCommand.AddArgument(nameArgument);
            exportCommand.AddArgument(fileArgument);

            exportCommand.SetHandler(async (string name, string file) =>
            {
                await HandleIndexExportAsync(name, file).ConfigureAwait(false);
            }, nameArgument, fileArgument);

            return exportCommand;
        }

        // Command handlers

        /// <summary>
        /// Handles the index create command
        /// </summary>
        private static async Task HandleIndexCreateAsync(string name, string storage, bool lemmatizer, bool stopWords, int minLength, int maxLength, string[]? tags, string[]? labels)
        {
            try
            {
                OutputManager.WriteVerbose($"Creating index '{name}' with storage mode '{storage}'");

                // Parse tags if provided
                Dictionary<string, string>? tagsDict = null;
                if (tags != null && tags.Length > 0)
                {
                    tagsDict = new Dictionary<string, string>();
                    foreach (string tag in tags)
                    {
                        int equalsIndex = tag.IndexOf('=');
                        if (equalsIndex <= 0 || equalsIndex >= tag.Length - 1)
                        {
                            OutputManager.WriteError($"Invalid tag format: '{tag}'. Expected key=value");
                            return;
                        }

                        string key = tag.Substring(0, equalsIndex);
                        string value = tag.Substring(equalsIndex + 1);
                        tagsDict[key] = value;
                    }
                }

                // Parse labels if provided
                List<string>? labelsList = null;
                if (labels != null && labels.Length > 0)
                {
                    labelsList = labels.Select(l => l.Trim().ToLowerInvariant()).ToList();
                }

                await IndexManager.Instance.CreateIndexAsync(name, storage, lemmatizer, stopWords, minLength, maxLength, tagsDict, labelsList).ConfigureAwait(false);

                OutputManager.WriteSuccess($"Index '{name}' created successfully");

                if (lemmatizer)
                    OutputManager.WriteInfo("Lemmatization enabled");
                if (stopWords)
                    OutputManager.WriteInfo("Stop word removal enabled");
                if (minLength > 0)
                    OutputManager.WriteInfo($"Minimum token length: {minLength}");
                if (maxLength > 0)
                    OutputManager.WriteInfo($"Maximum token length: {maxLength}");
                if (tagsDict != null && tagsDict.Count > 0)
                    OutputManager.WriteInfo($"Tags: {string.Join(", ", tagsDict.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                if (labelsList != null && labelsList.Count > 0)
                    OutputManager.WriteInfo($"Labels: {string.Join(", ", labelsList)}");
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to create index '{name}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the index list command
        /// </summary>
        private static Task HandleIndexListAsync()
        {
            try
            {
                OutputManager.WriteVerbose("Listing available indices");

                object[] indices = IndexManager.Instance.ListIndices();
                OutputManager.WriteData(indices);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to list indices: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the index use command
        /// </summary>
        private static async Task HandleIndexUseAsync(string name)
        {
            try
            {
                OutputManager.WriteVerbose($"Switching to index '{name}'");

                await IndexManager.Instance.UseIndexAsync(name).ConfigureAwait(false);
                OutputManager.WriteSuccess($"Now using index '{name}'");
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to switch to index '{name}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the index delete command
        /// </summary>
        private static async Task HandleIndexDeleteAsync(string name, bool force)
        {
            try
            {
                if (!force)
                {
                    OutputManager.WriteLine($"Are you sure you want to delete index '{name}'? (y/N)");
                    string? response = Console.ReadLine();
                    if (response?.ToLowerInvariant() != "y" && response?.ToLowerInvariant() != "yes")
                    {
                        OutputManager.WriteLine("Operation cancelled");
                        return;
                    }
                }

                OutputManager.WriteVerbose($"Deleting index '{name}'");

                await IndexManager.Instance.DeleteIndexAsync(name).ConfigureAwait(false);
                OutputManager.WriteSuccess($"Index '{name}' deleted successfully");
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to delete index '{name}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the index info command
        /// </summary>
        private static async Task HandleIndexInfoAsync(string? name)
        {
            try
            {
                string indexName = name ?? IndexManager.Instance.CurrentIndexName ?? "current";
                OutputManager.WriteVerbose($"Showing information for index '{indexName}'");

                object info = await IndexManager.Instance.GetIndexInfoAsync(name).ConfigureAwait(false);
                OutputManager.WriteData(info);
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to get index information: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the index export command
        /// </summary>
        private static async Task HandleIndexExportAsync(string name, string file)
        {
            try
            {
                OutputManager.WriteVerbose($"Exporting index '{name}' to '{file}'");

                object stats = await IndexManager.Instance.GetStatisticsAsync(name).ConfigureAwait(false);

                var exportData = new
                {
                    Timestamp = DateTime.UtcNow,
                    IndexName = name,
                    Statistics = stats,
                    Configuration = IndexManager.Instance.Configurations.ContainsKey(name)
                        ? IndexManager.Instance.Configurations[name]
                        : null
                };

                string json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(file, json).ConfigureAwait(false);
                OutputManager.WriteSuccess($"Index '{name}' exported to '{file}'");
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to export index '{name}': {ex.Message}");
                throw;
            }
        }
    }
}