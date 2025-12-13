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
    /// Commands for managing documents in Verbex indices
    /// </summary>
    public static class DocumentCommands
    {
        /// <summary>
        /// Creates the document command group
        /// </summary>
        /// <returns>Document command</returns>
        public static Command CreateDocumentCommand()
        {
            Command docCommand = new Command("doc", "Manage documents in indices");

            docCommand.AddCommand(CreateDocumentAddCommand());
            docCommand.AddCommand(CreateDocumentRemoveCommand());
            docCommand.AddCommand(CreateDocumentListCommand());
            docCommand.AddCommand(CreateDocumentClearCommand());

            return docCommand;
        }

        /// <summary>
        /// Creates the unified document add command
        /// </summary>
        /// <returns>Document add command</returns>
        private static Command CreateDocumentAddCommand()
        {
            Command addCommand = new Command("add", "Add a document (use --content or --file)");

            Argument<string> nameArgument = new Argument<string>("name", "Document name");

            Option<string> indexOption = new Option<string>(
                aliases: new[] { "--index", "-i" },
                description: "Index name (uses active index if not specified)")
            {
                IsRequired = false
            };

            Option<string> contentOption = new Option<string>(
                aliases: new[] { "--content", "-c" },
                description: "Document content (mutually exclusive with --file)")
            {
                IsRequired = false
            };

            Option<string> fileOption = new Option<string>(
                aliases: new[] { "--file", "-f" },
                description: "Load content from file (mutually exclusive with --content)")
            {
                IsRequired = false
            };

            Option<string[]> metadataOption = new Option<string[]>(
                aliases: new[] { "--meta", "-m", "--tag", "-t" },
                description: "Tags in key=value format (repeatable)")
            {
                IsRequired = false,
                AllowMultipleArgumentsPerToken = true
            };

            Option<string[]> labelOption = new Option<string[]>(
                aliases: new[] { "--label", "-L" },
                description: "Labels to associate with the document (repeatable)")
            {
                IsRequired = false,
                AllowMultipleArgumentsPerToken = true
            };

            addCommand.AddArgument(nameArgument);
            addCommand.AddOption(indexOption);
            addCommand.AddOption(contentOption);
            addCommand.AddOption(fileOption);
            addCommand.AddOption(metadataOption);
            addCommand.AddOption(labelOption);

            addCommand.SetHandler(async (string name, string? index, string? content, string? file, string[]? metadata, string[]? labels) =>
            {
                await HandleDocumentAddAsync(index, name, content, file, metadata, labels).ConfigureAwait(false);
            }, nameArgument, indexOption, contentOption, fileOption, metadataOption, labelOption);

            return addCommand;
        }

        /// <summary>
        /// Creates the document remove command
        /// </summary>
        /// <returns>Document remove command</returns>
        private static Command CreateDocumentRemoveCommand()
        {
            Command removeCommand = new Command("remove", "Remove a document");

            Argument<string> nameArgument = new Argument<string>("name", "Document name");

            Option<string> indexOption = new Option<string>(
                aliases: new[] { "--index", "-i" },
                description: "Index name (uses active index if not specified)")
            {
                IsRequired = false
            };

            removeCommand.AddArgument(nameArgument);
            removeCommand.AddOption(indexOption);

            removeCommand.SetHandler(async (string name, string? index) =>
            {
                await HandleDocumentRemoveAsync(index, name).ConfigureAwait(false);
            }, nameArgument, indexOption);

            return removeCommand;
        }

        /// <summary>
        /// Creates the document list command
        /// </summary>
        /// <returns>Document list command</returns>
        private static Command CreateDocumentListCommand()
        {
            Command listCommand = new Command("ls", "List documents in an index");

            Option<string> indexOption = new Option<string>(
                aliases: new[] { "--index", "-i" },
                description: "Index name (uses active index if not specified)")
            {
                IsRequired = false
            };

            listCommand.AddOption(indexOption);

            listCommand.SetHandler(async (string? index) =>
            {
                await HandleDocumentListAsync(index).ConfigureAwait(false);
            }, indexOption);

            return listCommand;
        }

        /// <summary>
        /// Creates the document clear command
        /// </summary>
        /// <returns>Document clear command</returns>
        private static Command CreateDocumentClearCommand()
        {
            Command clearCommand = new Command("clear", "Clear all documents from an index");

            Option<string> indexOption = new Option<string>(
                aliases: new[] { "--index", "-i" },
                description: "Index name (uses active index if not specified)")
            {
                IsRequired = false
            };

            Option<bool> forceOption = new Option<bool>(
                aliases: new[] { "--force" },
                description: "Force clearing without confirmation")
            {
                IsRequired = false
            };

            clearCommand.AddOption(indexOption);
            clearCommand.AddOption(forceOption);

            clearCommand.SetHandler(async (string? index, bool force) =>
            {
                await HandleDocumentClearAsync(index, force).ConfigureAwait(false);
            }, indexOption, forceOption);

            return clearCommand;
        }

        /// <summary>
        /// Handles the unified document add command
        /// </summary>
        private static async Task HandleDocumentAddAsync(string? index, string name, string? content, string? file, string[]? metadata, string[]? labels)
        {
            try
            {
                // Validate: must have either content or file, not both
                if (content == null && file == null)
                {
                    OutputManager.WriteError("Must specify --content or --file");
                    return;
                }

                if (content != null && file != null)
                {
                    OutputManager.WriteError("Cannot specify both --content and --file");
                    return;
                }

                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");

                // Load content from file if specified
                string documentContent;
                if (file != null)
                {
                    if (!File.Exists(file))
                    {
                        OutputManager.WriteError($"File not found: {file}");
                        return;
                    }

                    documentContent = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                    OutputManager.WriteVerbose($"Adding document '{name}' from file '{file}' to index '{actualIndex}'");
                }
                else
                {
                    documentContent = content!;
                    OutputManager.WriteVerbose($"Adding document '{name}' to index '{actualIndex}'");
                }

                // Parse metadata/tags if provided
                Dictionary<string, object>? metadataDict = null;
                if (metadata != null && metadata.Length > 0)
                {
                    metadataDict = new Dictionary<string, object>();
                    foreach (string meta in metadata)
                    {
                        int equalsIndex = meta.IndexOf('=');
                        if (equalsIndex <= 0 || equalsIndex >= meta.Length - 1)
                        {
                            OutputManager.WriteError($"Invalid tag format: '{meta}'. Expected key=value");
                            return;
                        }

                        string key = meta.Substring(0, equalsIndex);
                        string value = meta.Substring(equalsIndex + 1);

                        // Try to parse as number if possible, otherwise keep as string
                        object parsedValue;
                        if (int.TryParse(value, out int intValue))
                        {
                            parsedValue = intValue;
                        }
                        else if (double.TryParse(value, out double doubleValue))
                        {
                            parsedValue = doubleValue;
                        }
                        else if (bool.TryParse(value, out bool boolValue))
                        {
                            parsedValue = boolValue;
                        }
                        else
                        {
                            parsedValue = value;
                        }

                        metadataDict[key] = parsedValue;
                    }
                }

                // Parse labels if provided
                List<string>? labelsList = null;
                if (labels != null && labels.Length > 0)
                {
                    labelsList = labels.Select(l => l.Trim().ToLowerInvariant()).ToList();
                }

                await IndexManager.Instance.AddDocumentAsync(actualIndex, name, documentContent, metadataDict, labelsList).ConfigureAwait(false);
                OutputManager.WriteSuccess($"Document '{name}' added to index '{actualIndex}'");
                OutputManager.WriteInfo($"Content length: {documentContent.Length} characters");

                if (metadataDict != null && metadataDict.Count > 0)
                {
                    OutputManager.WriteInfo($"Tags: {string.Join(", ", metadataDict.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                }

                if (labelsList != null && labelsList.Count > 0)
                {
                    OutputManager.WriteInfo($"Labels: {string.Join(", ", labelsList)}");
                }
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to add document '{name}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the document remove command
        /// </summary>
        private static async Task HandleDocumentRemoveAsync(string? index, string name)
        {
            try
            {
                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");
                OutputManager.WriteVerbose($"Removing document '{name}' from index '{actualIndex}'");

                await IndexManager.Instance.RemoveDocumentAsync(actualIndex, name).ConfigureAwait(false);
                OutputManager.WriteSuccess($"Document '{name}' removed from index '{actualIndex}'");
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to remove document '{name}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the document list command
        /// </summary>
        private static async Task HandleDocumentListAsync(string? index)
        {
            try
            {
                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");
                OutputManager.WriteVerbose($"Listing documents in index '{actualIndex}'");

                object[] documents = await IndexManager.Instance.ListDocumentsAsync(actualIndex).ConfigureAwait(false);
                OutputManager.WriteData(documents);
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to list documents: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Handles the document clear command
        /// </summary>
        private static async Task HandleDocumentClearAsync(string? index, bool force)
        {
            try
            {
                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");
                if (!force)
                {
                    OutputManager.WriteLine($"This will clear all documents from index '{actualIndex}'. Use --force to confirm.");
                    return;
                }

                OutputManager.WriteVerbose($"Clearing all documents from index '{actualIndex}'");

                object[] documents = await IndexManager.Instance.ListDocumentsAsync(actualIndex).ConfigureAwait(false);
                foreach (dynamic doc in documents)
                {
                    await IndexManager.Instance.RemoveDocumentAsync(actualIndex, doc.Name).ConfigureAwait(false);
                }

                OutputManager.WriteSuccess($"All documents cleared from index '{actualIndex}'");
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Failed to clear documents: {ex.Message}");
                throw;
            }
        }
    }
}
