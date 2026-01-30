namespace Verbex.Database.Mysql
{
    using System;

    /// <summary>
    /// Provides SQL sanitization methods for MySQL to prevent SQL injection.
    /// </summary>
    internal static class Sanitizer
    {
        /// <summary>
        /// Sanitizes a string value for use in SQL queries by escaping single quotes and backslashes.
        /// </summary>
        /// <param name="value">The value to sanitize.</param>
        /// <returns>The sanitized value with special characters escaped.</returns>
        public static string Sanitize(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'");
        }

        /// <summary>
        /// Sanitizes a column or table name for use in SQL queries.
        /// </summary>
        /// <param name="name">The name to sanitize.</param>
        /// <returns>The sanitized name.</returns>
        /// <exception cref="ArgumentException">Thrown when name contains invalid characters.</exception>
        public static string SanitizeIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("Identifier cannot be null or empty.", nameof(name));
            }

            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    throw new ArgumentException($"Identifier contains invalid character: '{c}'", nameof(name));
                }
            }

            return name;
        }

        /// <summary>
        /// Escapes a LIKE pattern by escaping wildcard characters.
        /// </summary>
        /// <param name="pattern">The pattern to escape.</param>
        /// <returns>The escaped pattern.</returns>
        public static string EscapeLikePattern(string? pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return string.Empty;
            }

            return pattern
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("%", "\\%")
                .Replace("_", "\\_");
        }

        /// <summary>
        /// Formats a boolean value for MySQL.
        /// </summary>
        /// <param name="value">The boolean value.</param>
        /// <returns>"1" for true, "0" for false.</returns>
        public static string FormatBoolean(bool value)
        {
            return value ? "1" : "0";
        }

        /// <summary>
        /// Formats a DateTime value for MySQL using ISO 8601 format.
        /// </summary>
        /// <param name="value">The DateTime value.</param>
        /// <returns>Formatted string suitable for MySQL DATETIME.</returns>
        public static string FormatDateTime(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// Formats a nullable string value for SQL, returning NULL or quoted string.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <returns>"NULL" or a single-quoted, escaped string.</returns>
        public static string FormatNullableString(string? value)
        {
            if (value == null)
            {
                return "NULL";
            }

            return $"'{Sanitize(value)}'";
        }

        /// <summary>
        /// Formats a nullable integer value for SQL.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <returns>"NULL" or the integer as a string.</returns>
        public static string FormatNullableInt(int? value)
        {
            return value.HasValue ? value.Value.ToString() : "NULL";
        }

        /// <summary>
        /// Formats a nullable decimal value for SQL.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <returns>"NULL" or the decimal as a string.</returns>
        public static string FormatNullableDecimal(decimal? value)
        {
            return value.HasValue ? value.Value.ToString(System.Globalization.CultureInfo.InvariantCulture) : "NULL";
        }
    }
}
