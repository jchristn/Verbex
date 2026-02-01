namespace Verbex.Sdk.DTO.Requests
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a document to be added in a batch operation.
    /// </summary>
    public class BatchAddDocumentItem
    {
        /// <summary>
        /// Optional document ID (auto-generated if not provided).
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Document name/path.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Document content to index.
        /// </summary>
        public string Content { get; set; } = string.Empty;

        /// <summary>
        /// Optional labels for the document.
        /// </summary>
        public List<string>? Labels { get; set; }

        /// <summary>
        /// Optional tags (key-value pairs) for the document.
        /// </summary>
        public Dictionary<string, string>? Tags { get; set; }

        /// <summary>
        /// Optional custom metadata (any JSON-serializable value).
        /// </summary>
        public object? CustomMetadata { get; set; }

        /// <summary>
        /// Creates a new batch add document item.
        /// </summary>
        public BatchAddDocumentItem()
        {
        }

        /// <summary>
        /// Creates a new batch add document item with name and content.
        /// </summary>
        /// <param name="name">Document name.</param>
        /// <param name="content">Document content.</param>
        public BatchAddDocumentItem(string name, string content)
        {
            Name = name;
            Content = content;
        }

        /// <summary>
        /// Creates a new batch add document item with ID, name, and content.
        /// </summary>
        /// <param name="id">Document ID.</param>
        /// <param name="name">Document name.</param>
        /// <param name="content">Document content.</param>
        public BatchAddDocumentItem(string id, string name, string content)
        {
            Id = id;
            Name = name;
            Content = content;
        }
    }
}
