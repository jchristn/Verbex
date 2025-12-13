namespace Verbex.Repositories.Queries
{
    /// <summary>
    /// Contains SQL queries for label operations.
    /// Labels can be associated with documents (document_id not null) or with the index itself (document_id is null).
    /// </summary>
    internal static class LabelQueries
    {
        /// <summary>
        /// Insert a label.
        /// </summary>
        internal static string Insert()
        {
            return @"
                INSERT INTO labels (id, document_id, label, last_modified_utc, created_utc)
                VALUES (@id, @documentId, @label, @lastModifiedUtc, @createdUtc);
            ";
        }

        /// <summary>
        /// Insert a label if it doesn't exist.
        /// </summary>
        internal static string InsertIfNotExists()
        {
            return @"
                INSERT INTO labels (id, document_id, label, last_modified_utc, created_utc)
                SELECT @id, @documentId, @label, @lastModifiedUtc, @createdUtc
                WHERE NOT EXISTS (
                    SELECT 1 FROM labels
                    WHERE (document_id = @documentId OR (document_id IS NULL AND @documentId IS NULL))
                    AND label = @label
                );
            ";
        }

        /// <summary>
        /// Select labels by document ID.
        /// </summary>
        internal static string SelectByDocumentId()
        {
            return "SELECT * FROM labels WHERE document_id = @documentId ORDER BY label;";
        }

        /// <summary>
        /// Select index-level labels (document_id is null).
        /// </summary>
        internal static string SelectIndexLabels()
        {
            return "SELECT * FROM labels WHERE document_id IS NULL ORDER BY label;";
        }

        /// <summary>
        /// Select all labels with pagination.
        /// </summary>
        internal static string SelectAll()
        {
            return "SELECT * FROM labels ORDER BY document_id, label LIMIT @limit OFFSET @offset;";
        }

        /// <summary>
        /// Select distinct labels.
        /// </summary>
        internal static string SelectDistinct()
        {
            return "SELECT DISTINCT label FROM labels ORDER BY label;";
        }

        /// <summary>
        /// Select documents with a specific label.
        /// </summary>
        internal static string SelectDocumentsByLabel()
        {
            return @"
                SELECT d.*
                FROM documents d
                INNER JOIN labels l ON d.id = l.document_id
                WHERE l.label = @label
                ORDER BY d.indexed_utc DESC;
            ";
        }

        /// <summary>
        /// Select documents with all specified labels (AND).
        /// </summary>
        /// <param name="labelCount">Number of labels.</param>
        /// <returns>SQL string.</returns>
        internal static string SelectDocumentsWithAllLabels(int labelCount)
        {
            string[] parameters = new string[labelCount];
            for (int i = 0; i < labelCount; i++)
            {
                parameters[i] = $"@label{i}";
            }
            return $@"
                SELECT d.*
                FROM documents d
                INNER JOIN labels l ON d.id = l.document_id
                WHERE l.label IN ({string.Join(", ", parameters)})
                GROUP BY d.id
                HAVING COUNT(DISTINCT l.label) = {labelCount}
                ORDER BY d.indexed_utc DESC;
            ";
        }

        /// <summary>
        /// Check if a label exists for a document.
        /// </summary>
        internal static string Exists()
        {
            return @"
                SELECT 1 FROM labels
                WHERE (document_id = @documentId OR (document_id IS NULL AND @documentId IS NULL))
                AND label = @label
                LIMIT 1;
            ";
        }

        /// <summary>
        /// Check if an index-level label exists.
        /// </summary>
        internal static string ExistsIndexLabel()
        {
            return "SELECT 1 FROM labels WHERE document_id IS NULL AND label = @label LIMIT 1;";
        }

        /// <summary>
        /// Count labels for a document.
        /// </summary>
        internal static string CountByDocument()
        {
            return "SELECT COUNT(*) FROM labels WHERE document_id = @documentId;";
        }

        /// <summary>
        /// Count index-level labels.
        /// </summary>
        internal static string CountIndexLabels()
        {
            return "SELECT COUNT(*) FROM labels WHERE document_id IS NULL;";
        }

        /// <summary>
        /// Count documents with a specific label.
        /// </summary>
        internal static string CountDocumentsByLabel()
        {
            return "SELECT COUNT(DISTINCT document_id) FROM labels WHERE label = @label AND document_id IS NOT NULL;";
        }

        /// <summary>
        /// Delete a specific label from a document.
        /// </summary>
        internal static string DeleteByDocumentAndLabel()
        {
            return @"
                DELETE FROM labels
                WHERE (document_id = @documentId OR (document_id IS NULL AND @documentId IS NULL))
                AND label = @label;
            ";
        }

        /// <summary>
        /// Delete all labels from a document.
        /// </summary>
        internal static string DeleteByDocumentId()
        {
            return "DELETE FROM labels WHERE document_id = @documentId;";
        }

        /// <summary>
        /// Delete an index-level label.
        /// </summary>
        internal static string DeleteIndexLabel()
        {
            return "DELETE FROM labels WHERE document_id IS NULL AND label = @label;";
        }

        /// <summary>
        /// Delete all index-level labels.
        /// </summary>
        internal static string DeleteAllIndexLabels()
        {
            return "DELETE FROM labels WHERE document_id IS NULL;";
        }

        /// <summary>
        /// Delete all labels.
        /// </summary>
        internal static string DeleteAll()
        {
            return "DELETE FROM labels;";
        }

        /// <summary>
        /// Batch insert labels for a document.
        /// </summary>
        /// <param name="count">Number of labels to insert.</param>
        /// <returns>SQL string.</returns>
        internal static string InsertBatch(int count)
        {
            List<string> values = new List<string>();
            for (int i = 0; i < count; i++)
            {
                values.Add($"(@id{i}, @documentId, @label{i}, @lastModifiedUtc, @createdUtc)");
            }
            return $@"
                INSERT OR IGNORE INTO labels (id, document_id, label, last_modified_utc, created_utc)
                VALUES {string.Join(", ", values)};
            ";
        }
    }
}
