namespace Verbex.Database.SqlServer
{
    using System;

    /// <summary>
    /// Provides SQL sanitization methods for SQL Server to prevent SQL injection.
    /// </summary>
    internal static class Sanitizer
    {
        /// <summary>
        /// Sanitizes a string value for use in SQL queries by escaping single quotes.
        /// </summary>
        /// <param name="value">The value to sanitize.</param>
        /// <returns>The sanitized value with single quotes escaped.</returns>
        public static string Sanitize(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("'", "''");
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
                .Replace("'", "''")
                .Replace("[", "[[]")
                .Replace("%", "[%]")
                .Replace("_", "[_]");
        }

        /// <summary>
        /// Formats a boolean value for SQL Server.
        /// </summary>
        /// <param name="value">The boolean value.</param>
        /// <returns>"1" for true, "0" for false.</returns>
        public static string FormatBoolean(bool value)
        {
            return value ? "1" : "0";
        }

        /// <summary>
        /// Formats a DateTime value for SQL Server using ISO 8601 format.
        /// </summary>
        /// <param name="value">The DateTime value.</param>
        /// <returns>Formatted string suitable for SQL Server DATETIME2.</returns>
        public static string FormatDateTime(DateTime value)
        {
            return value.ToString("yyyy-MM-ddTHH:mm:ss.fffffff");
        }

        /// <summary>
        /// Formats a nullable string value for SQL, returning NULL or quoted string.
        /// </summary>
        /// <param name="value">The value to format.</param>
        /// <returns>"NULL" or an N-prefixed single-quoted, escaped string.</returns>
        public static string FormatNullableString(string? value)
        {
            if (value == null)
            {
                return "NULL";
            }

            return $"N'{Sanitize(value)}'";
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
