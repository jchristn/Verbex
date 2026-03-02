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
                description: "Tag filters in key=value format (can be specified multiple times)")
            {
                IsRequired = false,
                AllowMultipleArgumentsPerToken = true
            };

            Option<string[]> labelOption = new Option<string[]>(
                aliases: new[] { "--label", "-L" },
                description: "Label filters (can be specified multiple times)")
            {
                IsRequired = false,
                AllowMultipleArgumentsPerToken = true
            };

            searchCommand.AddArgument(queryArgument);
            searchCommand.AddOption(indexOption);
            searchCommand.AddOption(andOption);
            searchCommand.AddOption(limitOption);
            searchCommand.AddOption(filterOption);
            searchCommand.AddOption(labelOption);

            searchCommand.SetHandler(async (string query, string? index, bool useAnd, int limit, string[]? filters, string[]? labels) =>
            {
                await HandleSearchAsync(index, query, useAnd, limit, filters, labels).ConfigureAwait(false);
            }, queryArgument, indexOption, andOption, limitOption, filterOption, labelOption);

            return searchCommand;
        }

        /// <summary>
        /// Handles the search command
        /// </summary>
        private static async Task HandleSearchAsync(string? index, string query, bool useAnd, int limit, string[]? filters, string[]? labels)
        {
            try
            {
                string actualIndex = index ?? IndexManager.Instance.CurrentIndexName ?? throw new InvalidOperationException("No index specified and no active index set. Use 'vbx index use <name>' to set an active index.");
                string logic = useAnd ? "AND" : "OR";

                // Parse tag filters
                Dictionary<string, string>? tagFilters = null;
                if (filters != null && filters.Length > 0)
                {
                    tagFilters = new Dictionary<string, string>();
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
                        tagFilters[key] = value;
                    }
                }

                // Parse labels
                List<string>? labelsList = null;
                if (labels != null && labels.Length > 0)
                {
                    labelsList = labels.Select(l => l.Trim().ToLowerInvariant()).ToList();
                }

                List<string> filterParts = new List<string>();
                if (tagFilters != null)
                {
                    filterParts.Add($"tags: {string.Join(", ", tagFilters.Select(kvp => $"{kvp.Key}={kvp.Value}"))}");
                }
                if (labelsList != null)
                {
                    filterParts.Add($"labels: {string.Join(", ", labelsList)}");
                }
                string filterDescription = filterParts.Count > 0
                    ? $" with {string.Join(" and ", filterParts)}"
                    : "";

                OutputManager.WriteVerbose($"Searching index '{actualIndex}' for '{query}' using {logic} logic (limit: {limit}){filterDescription}");

                object[] results = await IndexManager.Instance.SearchAsync(actualIndex, query, useAnd, limit, labelsList, tagFilters).ConfigureAwait(false);

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