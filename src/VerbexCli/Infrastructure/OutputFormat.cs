namespace VerbexCli.Infrastructure
{
    /// <summary>
    /// Available output formats for CLI responses
    /// </summary>
    public enum OutputFormat
    {
        /// <summary>
        /// Human-readable table format (default)
        /// </summary>
        Table,

        /// <summary>
        /// JSON format for programmatic consumption
        /// </summary>
        Json,

        /// <summary>
        /// CSV format for data analysis
        /// </summary>
        Csv,

        /// <summary>
        /// YAML format for configuration-style output
        /// </summary>
        Yaml
    }
}