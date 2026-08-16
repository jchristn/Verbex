namespace VerbexCli.Commands
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
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

            docCommand.Subcommands.Add(CreateDocumentAddCommand());
            docCommand.Subcommands.Add(CreateDocumentRemoveCommand());
            docCommand.Subcommands.Add(CreateDocumentListCommand());
            docCommand.Subcommands.Add(CreateDocumentClearCommand());

            return docCommand;
        }

        /// <summary>
        /// Creates the unified document add command
        /// </summary>
        /// <returns>Document add command</returns>
        private static Command CreateDocumentAddCommand()
        {
            Command addCommand = new Command("add", "Add a document (use --content or --file)");

            Argument<string> nameArgument = new Argument<string>("name") { Description = "Document name" };

            Option<string> indexOption = new Option<string>("--index", "-i")
            {
                Description = "Index name (uses active index if not specified)"
            };

            Option<string> contentOption = new Option<string>("--content", "-c")
            {
                Description = "Document content (mutually exclusive with --file)"
            };

            Option<string> fileOption = new Option<string>("--file", "-f")
            {
                Description = "Load content from file (mutually exclusive with --content)"
            };

            Option<string[]> metadataOption = new Option<string[]>("--meta", "-m", "--tag", "-t")
            {
                Description = "Tags in key=value format (repeatable)",
                AllowMultipleArgumentsPerToken = true
            };

            Option<string[]> labelOption = new Option<string[]>("--label", "-L")
            {
                Description = "Labels to associate with the document (repeatable)",
                AllowMultipleArgumentsPerToken = true
            };

            Option<string> customMetadataOption = new Option<string>("--custom-metadata", "-M")
            {
                Description = "Custom metadata as a JSON string (e.g., '{\"key\": \"value\"}')"
            };

            addCommand.Arguments.Add(nameArgument);
            addCommand.Options.Add(indexOption);
            addCommand.Options.Add(contentOption);
            addCommand.Options.Add(fileOption);
            addCommand.Options.Add(metadataOption);
            addCommand.Options.Add(labelOption);
            addCommand.Options.Add(customMetadataOption);

            addCommand.SetAction(async (ParseResult parseResult) =>
            {
                string name = parseResult.GetValue(nameArgument)!;
                string? index = parseResult.GetValue(indexOption);
                string? content = parseResult.GetValue(contentOption);
                string? file = parseResult.GetValue(fileOption);
                string[]? metadata = parseResult.GetValue(metadataOption);
                string[]? labels = parseResult.GetValue(labelOption);
                string? customMetadata = parseResult.GetValue(customMetadataOption);
                await HandleDocumentAddAsync(index, name, content, file, metadata, labels, customMetadata).ConfigureAwait(false);
            });

            return addCommand;
        }

        /// <summary>
        /// Creates the document remove command
        /// </summary>
        /// <returns>Document remove command</returns>
        private static Command CreateDocumentRemoveCommand()
        {
            Command removeCommand = new Command("remove", "Remove a document");

            Argument<string> nameArgument = new Argument<string>("name") { Description = "Document name" };

            Option<string> indexOption = new Option<string>("--index", "-i")
            {
                Description = "Index name (uses active index if not specified)"
            };

            removeCommand.Arguments.Add(nameArgument);
            removeCommand.Options.Add(indexOption);

            removeCommand.SetAction(async (ParseResult parseResult) =>
            {
                string name = parseResult.GetValue(nameArgument)!;
                string? index = parseResult.GetValue(indexOption);
                await HandleDocumentRemoveAsync(index, name).ConfigureAwait(false);
            });

            return removeCommand;
        }

        /// <summary>
        /// Creates the document list command
        /// </summary>
        /// <returns>Document list command</returns>
        private static Command CreateDocumentListCommand()
        {
            Command listCommand = new Command("ls", "List documents in an index");

            Option<string> indexOption = new Option<string>("--index", "-i")
            {
                Description = "Index name (uses active index if not specified)"
            };

            Option<string[]> labelOption = new Option<string[]>("--label", "-L")
            {
                Description = "Filter by label (can be specified multiple times)",
                AllowMultipleArgumentsPerToken = true
            };

            Option<string[]> tagOption = new Option<string[]>("--tag", "-t")
            {
                Description = "Filter by tag in key=value format (can be specified multiple times)",
                AllowMultipleArgumentsPerToken = true
            };

            listCommand.Options.Add(indexOption);
            listCommand.Options.Add(labelOption);
            listCommand.Options.Add(tagOption);

            listCommand.SetAction(async (ParseResult parseResult) =>
            {
                string? index = parseResult.GetValue(indexOption);
                string[]? labels = parseResult.GetValue(labelOption);
                string[]? tags = parseResult.GetValue(tagOption);
                await HandleDocumentListAsync(index, labels, tags).ConfigureAwait(false);
            });

            return listCommand;
        }

        /// <summary>
        /// Creates the document clear command
        /// </summary>
        /// <returns>Document clear command</returns>
        private static Command CreateDocumentClearCommand()
        {
            Command clearCommand = new Command("clear", "Clear all documents from an index");

            Option<string> indexOption = new Option<string>("--index", "-i")
            {
                Description = "Index name (uses active index if not specified)"
            };

            Option<bool> forceOption = new Option<bool>("--force")
            {
                Description = "Force clearing without confirmation"
            };

            clearCommand.Options.Add(indexOption);
            clearCommand.Options.Add(forceOption);

            clearCommand.SetAction(async (ParseResult parseResult) =>
            {
                string? index = parseResult.GetValue(indexOption);
                bool force = parseResult.GetValue(forceOption);
                await HandleDocumentClearAsync(index, force).ConfigureAwait(false);
            });

            return clearCommand;
        }

        /// <summary>
        /// Handles the unified document add command
        /// </summary>
        private static async Task HandleDocumentAddAsync(string? index, string name, string? content, string? file, string[]? metadata, string[]? labels, string? customMetadata)
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

                // Parse custom metadata JSON if provided
                object? parsedCustomMetadata = null;
                if (!string.IsNullOrWhiteSpace(customMetadata))
                {
                    try
                    {
                        parsedCustomMetadata = JsonSerializer.Deserialize<object>(customMetadata);
                    }
                    catch (JsonException ex)
                    {
                        OutputManager.WriteError($"Invalid custom metadata JSON: {ex.Message}");
                        return;
                    }
                }

                await IndexManager.Instance.AddDocumentAsync(actualIndex, name, documentContent, metadataDict, labelsList, parsedCustomMetadata).ConfigureAwait(false);
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

                if (parsedCustomMetadata != null)
                {
                    OutputManager.WriteInfo($"Custom metadata: {customMetadata}");
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
        private static async Task HandleDocumentListAsync(string? index, string[]? labels, string[]? tags)
        {
            try
            {
                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");
                OutputManager.WriteVerbose($"Listing documents in index '{actualIndex}'");

                // Parse labels
                List<string>? labelsList = null;
                if (labels != null && labels.Length > 0)
                {
                    labelsList = labels.Select(l => l.Trim().ToLowerInvariant()).ToList();
                }

                // Parse tag filters
                Dictionary<string, string>? tagFilters = null;
                if (tags != null && tags.Length > 0)
                {
                    tagFilters = new Dictionary<string, string>();
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
                        tagFilters[key] = value;
                    }
                }

                object[] documents;
                if (labelsList != null || tagFilters != null)
                {
                    documents = await IndexManager.Instance.ListDocumentsAsync(actualIndex, labelsList, tagFilters).ConfigureAwait(false);
                }
                else
                {
                    documents = await IndexManager.Instance.ListDocumentsAsync(actualIndex).ConfigureAwait(false);
                }

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
