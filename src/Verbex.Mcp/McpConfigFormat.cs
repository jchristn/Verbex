namespace Verbex.Mcp
{
    /// <summary>
    /// Describes the JSON shape an AI client uses to declare an MCP server, so the installer
    /// can write the correct entry for each supported client.
    /// </summary>
    public enum McpConfigFormat
    {
        /// <summary>
        /// A top-level <c>mcpServers</c> object whose entry carries <c>type</c> and <c>url</c>.
        /// Used by Claude Code and Codex.
        /// </summary>
        McpServersTypeUrl,

        /// <summary>
        /// A top-level <c>mcpServers</c> object whose entry carries only <c>url</c>.
        /// Used by Cursor.
        /// </summary>
        McpServersUrl,

        /// <summary>
        /// A top-level <c>mcpServers</c> object whose entry carries <c>httpUrl</c>.
        /// Used by the Gemini CLI.
        /// </summary>
        McpServersHttpUrl,

        /// <summary>
        /// A top-level <c>servers</c> array whose entry carries <c>name</c>, <c>transport</c>,
        /// a base <c>url</c>, and an <c>mcpPath</c>. Used by Mux.
        /// </summary>
        MuxServersArray
    }
}
