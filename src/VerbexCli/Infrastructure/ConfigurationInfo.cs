namespace VerbexCli.Infrastructure
{
    /// <summary>
    /// CLI configuration information for display.
    /// </summary>
    public class ConfigurationInfo
    {
        /// <summary>
        /// The currently active index name.
        /// </summary>
        public string CurrentIndex { get; set; } = string.Empty;

        /// <summary>
        /// Number of available indices.
        /// </summary>
        public int AvailableIndices { get; set; }

        /// <summary>
        /// Default output format.
        /// </summary>
        public string DefaultOutputFormat { get; set; } = string.Empty;

        /// <summary>
        /// Whether color output is enabled.
        /// </summary>
        public bool ColorEnabled { get; set; }

        /// <summary>
        /// Whether verbose output is enabled.
        /// </summary>
        public bool VerboseEnabled { get; set; }

        /// <summary>
        /// Whether quiet output is enabled.
        /// </summary>
        public bool QuietEnabled { get; set; }

        /// <summary>
        /// The effective configuration directory.
        /// </summary>
        public string ConfigDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Whether a custom configuration directory is being used.
        /// </summary>
        public bool IsCustomConfigDirectory { get; set; }
    }
}
