namespace VerbexCli.Commands
{
    using System;
    using System.Collections.Generic;
    using System.CommandLine;
    using System.Linq;
    using System.Threading.Tasks;
    using VerbexCli.Infrastructure;

    /// <summary>
    /// Commands for searching documents in Verbex indices
    /// </summary>
    public static class SearchCommands
    {
        /// <summary>
        /// Creates the search command
        /// </summary>
        /// <returns>Search command</returns>
        public static Command CreateSearchCommand()
        {
            Command searchCommand = new Command("search", "Search documents in an index");

            Argument<string> queryArgument = new Argument<string>("query", "Search query");

            Option<string> indexOption = new Option<string>(
                aliases: new[] { "--index", "-i" },
                description: "Index name (uses active index if not specified)")
            {
                IsRequired = false
            };

            Option<bool> andOption = new Option<bool>(
                aliases: new[] { "--and" },
                description: "Use AND logic (all terms must match)")
            {
                IsRequired = false
            };

            Option<int> limitOption = new Option<int>(
                aliases: new[] { "--limit", "-l" },
                description: "Maximum number of results")
            {
                IsRequired = false
            };
            limitOption.SetDefaultValue(10);

            Option<string[]> filterOption = new Option<string[]>(
                aliases: new[] { "--filter", "-f" },
                description: "Metadata filters in key=value format (can be specified multiple times)")
            {
                IsRequired = false,
                AllowMultipleArgumentsPerToken = true
            };

            searchCommand.AddArgument(queryArgument);
            searchCommand.AddOption(indexOption);
            searchCommand.AddOption(andOption);
            searchCommand.AddOption(limitOption);
            searchCommand.AddOption(filterOption);

            searchCommand.SetHandler(async (string query, string? index, bool useAnd, int limit, string[]? filters) =>
            {
                await HandleSearchAsync(index, query, useAnd, limit, filters).ConfigureAwait(false);
            }, queryArgument, indexOption, andOption, limitOption, filterOption);

            return searchCommand;
        }

        /// <summary>
        /// Handles the search command
        /// </summary>
        private static async Task HandleSearchAsync(string? index, string query, bool useAnd, int limit, string[]? filters)
        {
            try
            {
                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");
                string logic = useAnd ? "AND" : "OR";

                // Parse metadata filters
                Dictionary<string, object>? metadataFilters = null;
                if (filters != null && filters.Length > 0)
                {
                    metadataFilters = new Dictionary<string, object>();
                    foreach (string filter in filters)
                    {
                        int equalsIndex = filter.IndexOf('=');
                        if (equalsIndex <= 0 || equalsIndex >= filter.Length - 1)
                        {
                            OutputManager.WriteError($"Invalid filter format: '{filter}'. Expected key=value");
                            return;
                        }

                        string key = filter.Substring(0, equalsIndex);
                        string value = filter.Substring(equalsIndex + 1);

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

                        metadataFilters[key] = parsedValue;
                    }
                }

                string filterDescription = metadataFilters != null
                    ? $" with filters: {string.Join(", ", metadataFilters.Select(kvp => $"{kvp.Key}={kvp.Value}"))}"
                    : "";

                OutputManager.WriteVerbose($"Searching index '{actualIndex}' for '{query}' using {logic} logic (limit: {limit}){filterDescription}");

                object[] results = await IndexManager.Instance.SearchAsync(actualIndex, query, useAnd, limit, metadataFilters).ConfigureAwait(false);

                OutputManager.WriteInfo($"Found {results.Length} result(s) for query '{query}' using {logic} logic{filterDescription}");
                OutputManager.WriteData(results);
            }
            catch (Exception ex)
            {
                OutputManager.WriteError($"Search failed: {ex.Message}");
                throw;
            }
        }
    }
}