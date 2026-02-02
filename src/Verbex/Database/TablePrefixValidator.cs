namespace Verbex.Database
{
    using System;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Validates and sanitizes table prefixes for index-specific tables.
    /// </summary>
    /// <remarks>
    /// Table prefixes are used to isolate index data in a shared database.
    /// Each index gets its own set of tables with names like {prefix}_documents.
    /// </remarks>
    public static class TablePrefixValidator
    {
        /// <summary>
        /// Maximum length for a table prefix to ensure total table name stays within database limits.
        /// MySQL has a 64-character identifier limit. With suffix "_document_terms" (15 chars),
        /// we allow 48 characters for the prefix.
        /// </summary>
        public const int MaxPrefixLength = 48;

        /// <summary>
        /// Minimum length for a table prefix to ensure meaningful identifiers.
        /// </summary>
        public const int MinPrefixLength = 1;

        /// <summary>
        /// Pattern for valid table prefixes: alphanumeric and underscore only, must start with letter or underscore.
        /// </summary>
        private static readonly Regex _ValidPrefixPattern = new Regex(
            @"^[a-zA-Z_][a-zA-Z0-9_]*$",
            RegexOptions.Compiled);

        /// <summary>
        /// Validates and returns the table prefix if valid.
        /// </summary>
        /// <param name="prefix">The table prefix to validate.</param>
        /// <returns>The validated prefix.</returns>
        /// <exception cref="ArgumentException">Thrown when the prefix is invalid.</exception>
        public static string Validate(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                throw new ArgumentException("Table prefix cannot be null or empty.", nameof(prefix));
            }

            if (prefix.Length < MinPrefixLength)
            {
                throw new ArgumentException(
                    $"Table prefix must be at least {MinPrefixLength} character(s). Got: {prefix.Length}",
                    nameof(prefix));
            }

            if (prefix.Length > MaxPrefixLength)
            {
                throw new ArgumentException(
                    $"Table prefix cannot exceed {MaxPrefixLength} characters. Got: {prefix.Length}",
                    nameof(prefix));
            }

            if (!_ValidPrefixPattern.IsMatch(prefix))
            {
                throw new ArgumentException(
                    "Table prefix must contain only alphanumeric characters and underscores, " +
                    $"and must start with a letter or underscore. Got: {prefix}",
                    nameof(prefix));
            }

            return prefix;
        }

        /// <summary>
        /// Checks if a table prefix is valid without throwing exceptions.
        /// </summary>
        /// <param name="prefix">The table prefix to check.</param>
        /// <returns>True if the prefix is valid, false otherwise.</returns>
        public static bool IsValid(string prefix)
        {
            if (string.IsNullOrWhiteSpace(prefix))
            {
                return false;
            }

            if (prefix.Length < MinPrefixLength || prefix.Length > MaxPrefixLength)
            {
                return false;
            }

            return _ValidPrefixPattern.IsMatch(prefix);
        }

        /// <summary>
        /// Creates a safe table prefix from an index identifier.
        /// Replaces invalid characters with underscores.
        /// </summary>
        /// <param name="indexId">The index identifier to convert.</param>
        /// <returns>A safe table prefix derived from the index ID.</returns>
        /// <exception cref="ArgumentException">Thrown when the resulting prefix is empty or too long.</exception>
        public static string FromIndexId(string indexId)
        {
            if (string.IsNullOrWhiteSpace(indexId))
            {
                throw new ArgumentException("Index ID cannot be null or empty.", nameof(indexId));
            }

            // Replace invalid characters with underscores
            string sanitized = Regex.Replace(indexId, @"[^a-zA-Z0-9_]", "_");

            // Ensure it starts with a letter or underscore
            if (sanitized.Length > 0 && char.IsDigit(sanitized[0]))
            {
                sanitized = "_" + sanitized;
            }

            // Truncate if too long
            if (sanitized.Length > MaxPrefixLength)
            {
                sanitized = sanitized.Substring(0, MaxPrefixLength);
            }

            if (string.IsNullOrEmpty(sanitized))
            {
                throw new ArgumentException(
                    $"Cannot create valid table prefix from index ID: {indexId}",
                    nameof(indexId));
            }

            return sanitized;
        }
    }
}
