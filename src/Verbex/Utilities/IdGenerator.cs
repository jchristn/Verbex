namespace Verbex.Utilities
{
    using System;
    using PrettyId;

    /// <summary>
    /// Provides centralized generation of k-sortable unique identifiers for all database entities.
    /// Uses PrettyId library to generate IDs that are both unique and chronologically sortable.
    /// </summary>
    /// <remarks>
    /// All generated IDs are exactly 32 characters in length including their entity-specific prefix.
    /// IDs include a timestamp component ensuring chronological sorting when queried.
    /// </remarks>
    public static class IdGenerator
    {
        /// <summary>
        /// Total length for all generated IDs including prefix.
        /// </summary>
        private const int TotalIdLength = 32;

        /// <summary>
        /// Prefix for document IDs.
        /// </summary>
        private const string DocumentPrefix = "doc_";

        /// <summary>
        /// Prefix for term IDs.
        /// </summary>
        private const string TermPrefix = "term_";

        /// <summary>
        /// Prefix for document-term mapping IDs.
        /// </summary>
        private const string DocumentTermPrefix = "docterm_";

        /// <summary>
        /// Prefix for label IDs.
        /// </summary>
        private const string LabelPrefix = "label_";

        /// <summary>
        /// Prefix for tag IDs.
        /// </summary>
        private const string TagPrefix = "tag_";

        /// <summary>
        /// Prefix for index metadata IDs.
        /// </summary>
        private const string IndexMetadataPrefix = "idx_";

        /// <summary>
        /// The PrettyId generator instance used for all ID generation.
        /// </summary>
        private static readonly PrettyId.IdGenerator Generator = new PrettyId.IdGenerator();

        /// <summary>
        /// Generates a new k-sortable unique identifier for a document.
        /// </summary>
        /// <returns>A 32-character k-sortable unique identifier with "doc_" prefix.</returns>
        public static string GenerateDocumentId()
        {
            int randomPartLength = TotalIdLength - DocumentPrefix.Length;
            return Generator.GenerateKSortable(DocumentPrefix, randomPartLength);
        }

        /// <summary>
        /// Generates a new k-sortable unique identifier for a term.
        /// </summary>
        /// <returns>A 32-character k-sortable unique identifier with "term_" prefix.</returns>
        public static string GenerateTermId()
        {
            int randomPartLength = TotalIdLength - TermPrefix.Length;
            return Generator.GenerateKSortable(TermPrefix, randomPartLength);
        }

        /// <summary>
        /// Generates a new k-sortable unique identifier for a document-term mapping.
        /// </summary>
        /// <returns>A 32-character k-sortable unique identifier with "docterm_" prefix.</returns>
        public static string GenerateDocumentTermId()
        {
            int randomPartLength = TotalIdLength - DocumentTermPrefix.Length;
            return Generator.GenerateKSortable(DocumentTermPrefix, randomPartLength);
        }

        /// <summary>
        /// Generates a new k-sortable unique identifier for a label.
        /// </summary>
        /// <returns>A 32-character k-sortable unique identifier with "label_" prefix.</returns>
        public static string GenerateLabelId()
        {
            int randomPartLength = TotalIdLength - LabelPrefix.Length;
            return Generator.GenerateKSortable(LabelPrefix, randomPartLength);
        }

        /// <summary>
        /// Generates a new k-sortable unique identifier for a tag.
        /// </summary>
        /// <returns>A 32-character k-sortable unique identifier with "tag_" prefix.</returns>
        public static string GenerateTagId()
        {
            int randomPartLength = TotalIdLength - TagPrefix.Length;
            return Generator.GenerateKSortable(TagPrefix, randomPartLength);
        }

        /// <summary>
        /// Generates a new k-sortable unique identifier for index metadata.
        /// </summary>
        /// <returns>A 32-character k-sortable unique identifier with "idx_" prefix.</returns>
        public static string GenerateIndexMetadataId()
        {
            int randomPartLength = TotalIdLength - IndexMetadataPrefix.Length;
            return Generator.GenerateKSortable(IndexMetadataPrefix, randomPartLength);
        }

        /// <summary>
        /// Generates a new k-sortable unique identifier without a prefix.
        /// </summary>
        /// <returns>A 32-character k-sortable unique identifier.</returns>
        /// <remarks>
        /// Prefer using the entity-specific methods (GenerateDocumentId, GenerateTermId, etc.)
        /// for better traceability. This method is provided for backward compatibility.
        /// </remarks>
        [Obsolete("Use entity-specific methods (GenerateDocumentId, GenerateTermId, etc.) for better traceability.")]
        public static string Generate()
        {
            return Generator.GenerateKSortable(string.Empty, TotalIdLength);
        }

        /// <summary>
        /// Generates a new k-sortable unique identifier with a custom prefix.
        /// </summary>
        /// <param name="prefix">The prefix to prepend to the generated ID.</param>
        /// <returns>A 32-character k-sortable unique identifier string with the specified prefix.</returns>
        /// <exception cref="ArgumentNullException">Thrown when prefix is null.</exception>
        /// <exception cref="ArgumentException">Thrown when prefix is 24 characters or longer.</exception>
        /// <remarks>
        /// Prefer using the entity-specific methods (GenerateDocumentId, GenerateTermId, etc.)
        /// for standard entity types. This method is provided for custom entity types.
        /// </remarks>
        public static string Generate(string prefix)
        {
            ArgumentNullException.ThrowIfNull(prefix, nameof(prefix));

            if (prefix.Length >= TotalIdLength - 8)
            {
                throw new ArgumentException($"Prefix must be less than {TotalIdLength - 8} characters to allow sufficient ID uniqueness.", nameof(prefix));
            }

            int randomPartLength = TotalIdLength - prefix.Length;
            return Generator.GenerateKSortable(prefix, randomPartLength);
        }
    }
}
