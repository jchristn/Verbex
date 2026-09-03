namespace Verbex.Mcp
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A supported AI client whose configuration file can be patched with the Verbex MCP server entry.
    /// </summary>
    public class McpClientTarget
    {
        #region Public-Members

        /// <summary>
        /// Human-readable client name used in console output. Never null.
        /// </summary>
        public string Name
        {
            get { return _Name; }
            set { _Name = value ?? throw new ArgumentNullException(nameof(Name)); }
        }

        /// <summary>
        /// Candidate configuration file paths in priority order. The first existing path is patched;
        /// when none exist, the first path is created. Never null or empty.
        /// </summary>
        public IReadOnlyList<string> ConfigPaths
        {
            get { return _ConfigPaths; }
            set
            {
                if (value == null || value.Count == 0)
                    throw new ArgumentException("At least one configuration path is required.", nameof(ConfigPaths));
                _ConfigPaths = value;
            }
        }

        /// <summary>
        /// The JSON shape this client uses to declare an MCP server.
        /// </summary>
        public McpConfigFormat Format { get; set; }

        #endregion

        #region Private-Members

        private string _Name = string.Empty;
        private IReadOnlyList<string> _ConfigPaths = Array.Empty<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="name">Human-readable client name. Cannot be null.</param>
        /// <param name="format">The JSON shape the client uses to declare an MCP server.</param>
        /// <param name="configPaths">Candidate configuration file paths in priority order. Cannot be null or empty.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="configPaths"/> is null or empty.</exception>
        public McpClientTarget(string name, McpConfigFormat format, IReadOnlyList<string> configPaths)
        {
            Name = name;
            Format = format;
            ConfigPaths = configPaths;
        }

        #endregion
    }
}
