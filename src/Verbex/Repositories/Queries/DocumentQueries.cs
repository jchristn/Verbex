namespace Verbex.Repositories.Queries
{
    /// <summary>
    /// Contains SQL queries for document operations.
    /// </summary>
    internal static class DocumentQueries
    {
        /// <summary>
        /// Insert a new document.
        /// </summary>
        internal static string Insert()
        {
            return @"
                INSERT INTO documents (id, name, content_sha256, document_length, term_count, indexed_utc, last_modified_utc, created_utc)
                VALUES (@id, @name, @contentSha256, @documentLength, @termCount, @indexedUtc, @lastModifiedUtc, @createdUtc);
            ";
        }

        /// <summary>
        /// Select a document by ID.
        /// </summary>
        internal static string SelectById()
        {
            return "SELECT * FROM documents WHERE id = @id;";
        }

        /// <summary>
        /// Select a document by name.
        /// </summary>
        internal static string SelectByName()
        {
            return "SELECT * FROM documents WHERE name = @name;";
        }

        /// <summary>
        /// Select a document by content SHA-256 hash.
        /// </summary>
        internal static string SelectByContentSha256()
        {
            return "SELECT * FROM documents WHERE content_sha256 = @contentSha256;";
        }

        /// <summary>
        /// Select all documents with pagination.
        /// </summary>
        internal static string SelectAll()
        {
            return "SELECT * FROM documents ORDER BY indexed_utc DESC LIMIT @limit OFFSET @offset;";
        }

        /// <summary>
        /// Select documents by IDs.
        /// </summary>
        /// <param name="count">Number of IDs to select.</param>
        /// <returns>SQL string with parameterized IN clause.</returns>
        internal static string SelectByIds(int count)
        {
            string[] parameters = new string[count];
            for (int i = 0; i < count; i++)
            {
                parameters[i] = $"@id{i}";
            }
            return $"SELECT * FROM documents WHERE id IN ({string.Join(", ", parameters)});";
        }

        /// <summary>
        /// Count total documents.
        /// </summary>
        internal static string Count()
        {
            return "SELECT COUNT(*) FROM documents;";
        }

        /// <summary>
        /// Check if document exists by ID.
        /// </summary>
        internal static string ExistsById()
        {
            return "SELECT 1 FROM documents WHERE id = @id LIMIT 1;";
        }

        /// <summary>
        /// Check if document exists by name.
        /// </summary>
        internal static string ExistsByName()
        {
            return "SELECT 1 FROM documents WHERE name = @name LIMIT 1;";
        }

        /// <summary>
        /// Update a document.
        /// </summary>
        internal static string Update()
        {
            return @"
                UPDATE documents
                SET name = @name,
                    content_sha256 = @contentSha256,
                    document_length = @documentLength,
                    term_count = @termCount,
                    last_modified_utc = @lastModifiedUtc
                WHERE id = @id;
            ";
        }

        /// <summary>
        /// Update document term count.
        /// </summary>
        internal static string UpdateTermCount()
        {
            return "UPDATE documents SET term_count = @termCount, last_modified_utc = @lastModifiedUtc WHERE id = @id;";
        }

        /// <summary>
        /// Delete a document by ID.
        /// </summary>
        internal static string DeleteById()
        {
            return "DELETE FROM documents WHERE id = @id;";
        }

        /// <summary>
        /// Delete a document by name.
        /// </summary>
        internal static string DeleteByName()
        {
            return "DELETE FROM documents WHERE name = @name;";
        }

        /// <summary>
        /// Delete all documents.
        /// </summary>
        internal static string DeleteAll()
        {
            return "DELETE FROM documents;";
        }

        /// <summary>
        /// Search documents by name pattern.
        /// </summary>
        internal static string SearchByName()
        {
            return "SELECT * FROM documents WHERE name LIKE @pattern ORDER BY indexed_utc DESC LIMIT @limit OFFSET @offset;";
        }

        /// <summary>
        /// Select a document with all metadata (labels, tags, terms) in a single query.
        /// Uses GROUP_CONCAT to aggregate related data.
        /// </summary>
        internal static string SelectByIdWithMetadata()
        {
            return @"
                SELECT
                    d.*,
                    GROUP_CONCAT(DISTINCT l.label) as labels_csv,
                    GROUP_CONCAT(DISTINCT t.key || '=' || COALESCE(t.value, '')) as tags_csv,
                    GROUP_CONCAT(DISTINCT tm.term) as terms_csv
                FROM documents d
                LEFT JOIN labels l ON d.id = l.document_id
                LEFT JOIN tags t ON d.id = t.document_id
                LEFT JOIN document_terms dt ON d.id = dt.document_id
                LEFT JOIN terms tm ON dt.term_id = tm.id
                WHERE d.id = @id
                GROUP BY d.id;
            ";
        }
    }
}
