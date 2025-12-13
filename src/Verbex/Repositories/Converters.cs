namespace Verbex.Repositories
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Text.Json;

    /// <summary>
    /// Provides conversion utilities between database rows and domain objects.
    /// </summary>
    internal static class Converters
    {
        /// <summary>
        /// ISO 8601 timestamp format used for database storage.
        /// </summary>
        internal const string TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = false
        };

        /// <summary>
        /// Formats a DateTime as an ISO 8601 UTC string.
        /// </summary>
        /// <param name="dateTime">The DateTime to format.</param>
        /// <returns>ISO 8601 formatted string.</returns>
        internal static string FormatTimestamp(DateTime dateTime)
        {
            return dateTime.ToUniversalTime().ToString(TimestampFormat);
        }

        /// <summary>
        /// Parses an ISO 8601 UTC string to DateTime.
        /// </summary>
        /// <param name="timestamp">The timestamp string to parse.</param>
        /// <returns>Parsed DateTime in UTC.</returns>
        internal static DateTime ParseTimestamp(string? timestamp)
        {
            if (string.IsNullOrEmpty(timestamp))
            {
                return DateTime.MinValue;
            }

            if (DateTime.TryParse(timestamp, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime result))
            {
                return result.ToUniversalTime();
            }

            return DateTime.MinValue;
        }

        /// <summary>
        /// Gets a string value from a DataRow, handling DBNull.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <param name="column">The column name.</param>
        /// <returns>String value or null.</returns>
        internal static string? GetString(DataRow row, string column)
        {
            if (!row.Table.Columns.Contains(column))
            {
                return null;
            }

            object value = row[column];
            if (value == DBNull.Value || value == null)
            {
                return null;
            }

            return value.ToString();
        }

        /// <summary>
        /// Gets a required string value from a DataRow.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <param name="column">The column name.</param>
        /// <returns>String value.</returns>
        /// <exception cref="InvalidOperationException">If value is null.</exception>
        internal static string GetRequiredString(DataRow row, string column)
        {
            string? value = GetString(row, column);
            if (value == null)
            {
                throw new InvalidOperationException($"Required column '{column}' is null.");
            }
            return value;
        }

        /// <summary>
        /// Gets an integer value from a DataRow, handling DBNull.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <param name="column">The column name.</param>
        /// <param name="defaultValue">Default value if null.</param>
        /// <returns>Integer value.</returns>
        internal static int GetInt(DataRow row, string column, int defaultValue = 0)
        {
            if (!row.Table.Columns.Contains(column))
            {
                return defaultValue;
            }

            object value = row[column];
            if (value == DBNull.Value || value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt32(value);
        }

        /// <summary>
        /// Gets a long value from a DataRow, handling DBNull.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <param name="column">The column name.</param>
        /// <param name="defaultValue">Default value if null.</param>
        /// <returns>Long value.</returns>
        internal static long GetLong(DataRow row, string column, long defaultValue = 0)
        {
            if (!row.Table.Columns.Contains(column))
            {
                return defaultValue;
            }

            object value = row[column];
            if (value == DBNull.Value || value == null)
            {
                return defaultValue;
            }

            return Convert.ToInt64(value);
        }

        /// <summary>
        /// Gets a double value from a DataRow, handling DBNull.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <param name="column">The column name.</param>
        /// <param name="defaultValue">Default value if null.</param>
        /// <returns>Double value.</returns>
        internal static double GetDouble(DataRow row, string column, double defaultValue = 0.0)
        {
            if (!row.Table.Columns.Contains(column))
            {
                return defaultValue;
            }

            object value = row[column];
            if (value == DBNull.Value || value == null)
            {
                return defaultValue;
            }

            return Convert.ToDouble(value);
        }

        /// <summary>
        /// Gets a GUID value from a DataRow.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <param name="column">The column name.</param>
        /// <returns>GUID value or Guid.Empty.</returns>
        internal static Guid GetGuid(DataRow row, string column)
        {
            string? value = GetString(row, column);
            if (string.IsNullOrEmpty(value))
            {
                return Guid.Empty;
            }

            if (Guid.TryParse(value, out Guid result))
            {
                return result;
            }

            return Guid.Empty;
        }

        /// <summary>
        /// Gets a nullable GUID value from a DataRow.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <param name="column">The column name.</param>
        /// <returns>GUID value or null.</returns>
        internal static Guid? GetNullableGuid(DataRow row, string column)
        {
            string? value = GetString(row, column);
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            if (Guid.TryParse(value, out Guid result))
            {
                return result;
            }

            return null;
        }

        /// <summary>
        /// Gets a DateTime value from a DataRow.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <param name="column">The column name.</param>
        /// <returns>DateTime value.</returns>
        internal static DateTime GetDateTime(DataRow row, string column)
        {
            string? value = GetString(row, column);
            return ParseTimestamp(value);
        }

        /// <summary>
        /// Gets a nullable DateTime value from a DataRow.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <param name="column">The column name.</param>
        /// <returns>DateTime value or null.</returns>
        internal static DateTime? GetNullableDateTime(DataRow row, string column)
        {
            string? value = GetString(row, column);
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            return ParseTimestamp(value);
        }

        /// <summary>
        /// Serializes a list of integers to a JSON array string.
        /// </summary>
        /// <param name="positions">The integer list to serialize.</param>
        /// <returns>JSON array string (e.g., "[1,5,9]").</returns>
        internal static string SerializeIntList(List<int> positions)
        {
            if (positions == null || positions.Count == 0)
            {
                return "[]";
            }
            return JsonSerializer.Serialize(positions, JsonOptions);
        }

        /// <summary>
        /// Deserializes a JSON array string to a list of integers.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>List of integers.</returns>
        internal static List<int> DeserializeIntList(string? json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return new List<int>();
            }

            try
            {
                List<int>? result = JsonSerializer.Deserialize<List<int>>(json, JsonOptions);
                return result ?? new List<int>();
            }
            catch
            {
                return new List<int>();
            }
        }

        /// <summary>
        /// Converts a DataRow to a DocumentRecord.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <returns>DocumentRecord instance.</returns>
        internal static DocumentRecord DocumentFromRow(DataRow row)
        {
            return new DocumentRecord
            {
                Id = GetRequiredString(row, "id"),
                Name = GetRequiredString(row, "name"),
                ContentSha256 = GetString(row, "content_sha256"),
                DocumentLength = GetInt(row, "document_length"),
                TermCount = GetInt(row, "term_count"),
                IndexedUtc = GetDateTime(row, "indexed_utc"),
                LastModifiedUtc = GetNullableDateTime(row, "last_modified_utc"),
                CreatedUtc = GetDateTime(row, "created_utc")
            };
        }

        /// <summary>
        /// Converts a DataRow to a TermRecord.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <returns>TermRecord instance.</returns>
        internal static TermRecord TermFromRow(DataRow row)
        {
            return new TermRecord
            {
                Id = GetRequiredString(row, "id"),
                Term = GetRequiredString(row, "term"),
                DocumentFrequency = GetInt(row, "document_frequency"),
                TotalFrequency = GetInt(row, "total_frequency"),
                LastUpdatedUtc = GetDateTime(row, "last_updated_utc"),
                CreatedUtc = GetDateTime(row, "created_utc")
            };
        }

        /// <summary>
        /// Converts a DataRow to a DocumentTermRecord.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <returns>DocumentTermRecord instance.</returns>
        internal static DocumentTermRecord DocumentTermFromRow(DataRow row)
        {
            return new DocumentTermRecord
            {
                Id = GetRequiredString(row, "id"),
                DocumentId = GetRequiredString(row, "document_id"),
                TermId = GetRequiredString(row, "term_id"),
                TermFrequency = GetInt(row, "term_frequency"),
                CharacterPositions = DeserializeIntList(GetString(row, "character_positions")),
                TermPositions = DeserializeIntList(GetString(row, "term_positions")),
                LastModifiedUtc = GetDateTime(row, "last_modified_utc"),
                CreatedUtc = GetDateTime(row, "created_utc"),
                Term = GetString(row, "term"),
                DocumentName = GetString(row, "document_name")
            };
        }

        /// <summary>
        /// Converts a DataRow to a LabelRecord.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <returns>LabelRecord instance.</returns>
        internal static LabelRecord LabelFromRow(DataRow row)
        {
            return new LabelRecord
            {
                Id = GetRequiredString(row, "id"),
                DocumentId = GetString(row, "document_id"),
                Label = GetRequiredString(row, "label"),
                LastModifiedUtc = GetDateTime(row, "last_modified_utc"),
                CreatedUtc = GetDateTime(row, "created_utc")
            };
        }

        /// <summary>
        /// Converts a DataRow to a TagRecord.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <returns>TagRecord instance.</returns>
        internal static TagRecord TagFromRow(DataRow row)
        {
            return new TagRecord
            {
                Id = GetRequiredString(row, "id"),
                DocumentId = GetString(row, "document_id"),
                Key = GetRequiredString(row, "key"),
                Value = GetString(row, "value"),
                LastModifiedUtc = GetDateTime(row, "last_modified_utc"),
                CreatedUtc = GetDateTime(row, "created_utc")
            };
        }

        /// <summary>
        /// Converts a DataRow to an IndexMetadataRecord.
        /// </summary>
        /// <param name="row">The DataRow.</param>
        /// <returns>IndexMetadataRecord instance.</returns>
        internal static IndexMetadataRecord IndexMetadataFromRow(DataRow row)
        {
            return new IndexMetadataRecord
            {
                Id = GetRequiredString(row, "id"),
                Name = GetRequiredString(row, "name"),
                LastModifiedUtc = GetDateTime(row, "last_modified_utc"),
                CreatedUtc = GetDateTime(row, "created_utc")
            };
        }
    }
}
