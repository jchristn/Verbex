namespace Verbex.Repositories
{
    using System;
    using System.Text;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Provides input sanitization utilities for database operations.
    /// </summary>
    internal static class Sanitizer
    {
        private static readonly Regex ControlCharRegex = new Regex(@"[\x00-\x08\x0B\x0C\x0E-\x1F]", RegexOptions.Compiled);
        private static readonly Regex SqlCommentRegex = new Regex(@"(--.*$|/\*[\s\S]*?\*/)", RegexOptions.Multiline | RegexOptions.Compiled);

        /// <summary>
        /// Sanitizes a string value for safe use in SQL operations.
        /// Removes control characters and SQL comments, escapes single quotes.
        /// </summary>
        /// <param name="value">The value to sanitize.</param>
        /// <returns>Sanitized string, or empty string if input is null.</returns>
        internal static string Sanitize(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string result = value;

            result = ControlCharRegex.Replace(result, string.Empty);

            result = SqlCommentRegex.Replace(result, string.Empty);

            result = result.Replace("'", "''");

            return result;
        }

        /// <summary>
        /// Sanitizes a GUID value, returning its string representation.
        /// </summary>
        /// <param name="guid">The GUID to sanitize.</param>
        /// <returns>String representation of the GUID.</returns>
        internal static string SanitizeGuid(Guid guid)
        {
            return guid.ToString();
        }

        /// <summary>
        /// Sanitizes a nullable GUID value.
        /// </summary>
        /// <param name="guid">The nullable GUID to sanitize.</param>
        /// <returns>String representation of the GUID, or null if input is null.</returns>
        internal static string? SanitizeGuid(Guid? guid)
        {
            return guid?.ToString();
        }

        /// <summary>
        /// Validates that a string is a valid GUID format.
        /// </summary>
        /// <param name="value">The string to validate.</param>
        /// <returns>True if valid GUID format.</returns>
        internal static bool IsValidGuid(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            return Guid.TryParse(value, out _);
        }

        /// <summary>
        /// Sanitizes a string for use in LIKE patterns.
        /// Escapes %, _, and [ characters.
        /// </summary>
        /// <param name="value">The value to sanitize.</param>
        /// <returns>Sanitized pattern string.</returns>
        internal static string SanitizeForLike(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            string result = Sanitize(value);

            result = result.Replace("%", "[%]");
            result = result.Replace("_", "[_]");
            result = result.Replace("[", "[[]");

            return result;
        }

        /// <summary>
        /// Creates a prefix search pattern for LIKE queries.
        /// </summary>
        /// <param name="prefix">The prefix to search for.</param>
        /// <returns>Pattern string for LIKE query.</returns>
        internal static string CreatePrefixPattern(string? prefix)
        {
            return SanitizeForLike(prefix) + "%";
        }

        /// <summary>
        /// Creates a contains search pattern for LIKE queries.
        /// </summary>
        /// <param name="substring">The substring to search for.</param>
        /// <returns>Pattern string for LIKE query.</returns>
        internal static string CreateContainsPattern(string? substring)
        {
            return "%" + SanitizeForLike(substring) + "%";
        }

        /// <summary>
        /// Validates and sanitizes an integer value within bounds.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="min">Minimum allowed value.</param>
        /// <param name="max">Maximum allowed value.</param>
        /// <param name="defaultValue">Default value if out of bounds.</param>
        /// <returns>Validated integer value.</returns>
        internal static int ValidateInt(int value, int min, int max, int defaultValue)
        {
            if (value < min || value > max)
            {
                return defaultValue;
            }
            return value;
        }

        /// <summary>
        /// Validates pagination parameters.
        /// </summary>
        /// <param name="limit">The limit value.</param>
        /// <param name="offset">The offset value.</param>
        /// <param name="maxLimit">Maximum allowed limit.</param>
        /// <returns>Tuple of validated (limit, offset).</returns>
        internal static (int Limit, int Offset) ValidatePagination(int limit, int offset, int maxLimit = 10000)
        {
            int validLimit = Math.Max(1, Math.Min(limit, maxLimit));
            int validOffset = Math.Max(0, offset);
            return (validLimit, validOffset);
        }
    }
}
