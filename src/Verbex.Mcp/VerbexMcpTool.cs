namespace Verbex.Mcp
{
    using System;
    using Voltaic.Core;

    /// <summary>
    /// A transport-agnostic Verbex MCP tool: name, description, JSON input schema, and handler.
    /// The same definitions are registered with every transport (stdio, HTTP, WebSocket) so that
    /// <c>tools/list</c> and <c>tools/call</c> behave identically regardless of transport.
    /// <para>Thread safety: instances are immutable after construction and safe to share.</para>
    /// </summary>
    public class VerbexMcpTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name as advertised in <c>tools/list</c>. Never null or empty.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Human- and model-readable description of the tool. Never null.
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// JSON schema object describing the tool's arguments. Never null.
        /// </summary>
        public object InputSchema { get; }

        /// <summary>
        /// Tool handler. Receives the tool arguments (may be null) and returns the tool result. Never null.
        /// </summary>
        public Func<RpcParameters?, object> Handler { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate a tool definition.
        /// </summary>
        /// <param name="name">Tool name. Must be non-empty.</param>
        /// <param name="description">Tool description. Must be non-null.</param>
        /// <param name="inputSchema">JSON schema object for the tool's arguments. Must be non-null.</param>
        /// <param name="handler">Tool handler. Must be non-null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is empty or whitespace.</exception>
        public VerbexMcpTool(string name, string description, object inputSchema, Func<RpcParameters?, object> handler)
        {
            ArgumentNullException.ThrowIfNull(name);
            ArgumentNullException.ThrowIfNull(description);
            ArgumentNullException.ThrowIfNull(inputSchema);
            ArgumentNullException.ThrowIfNull(handler);
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tool name must be non-empty.", nameof(name));

            Name = name;
            Description = description;
            InputSchema = inputSchema;
            Handler = handler;
        }

        #endregion
    }
}
