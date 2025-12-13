namespace Verbex.Repositories.Queries
{
    /// <summary>
    /// Contains SQL queries for tag (key-value) operations.
    /// Tags can be associated with documents (document_id not null) or with the index itself (document_id is null).
    /// </summary>
    internal static class TagQueries
    {
        /// <summary>
        /// Insert a tag.
        /// </summary>
        internal static string Insert()
        {
            return @"
                INSERT INTO tags (id, document_id, key, value, last_modified_utc, created_utc)
                VALUES (@id, @documentId, @key, @value, @lastModifiedUtc, @createdUtc);
            ";
        }

        /// <summary>
        /// Insert or replace a tag (upsert by document_id + key).
        /// </summary>
        internal static string InsertOrReplace()
        {
            return @"
                INSERT INTO tags (id, document_id, key, value, last_modified_utc, created_utc)
                VALUES (@id, @documentId, @key, @value, @lastModifiedUtc, @createdUtc)
                ON CONFLICT(document_id, key) DO UPDATE SET value = @value, last_modified_utc = @lastModifiedUtc;
            ";
        }

        /// <summary>
        /// Select tags by document ID.
        /// </summary>
        internal static string SelectByDocumentId()
        {
            return "SELECT * FROM tags WHERE document_id = @documentId ORDER BY key;";
        }

        /// <summary>
        /// Select index-level tags (document_id is null).
        /// </summary>
        internal static string SelectIndexTags()
        {
            return "SELECT * FROM tags WHERE document_id IS NULL ORDER BY key;";
        }

        /// <summary>
        /// Select a specific tag by document and key.
        /// </summary>
        internal static string SelectByDocumentAndKey()
        {
            return @"
                SELECT * FROM tags
                WHERE (document_id = @documentId OR (document_id IS NULL AND @documentId IS NULL))
                AND key = @key;
            ";
        }

        /// <summary>
        /// Select an index-level tag by key.
        /// </summary>
        internal static string SelectIndexTagByKey()
        {
            return "SELECT * FROM tags WHERE document_id IS NULL AND key = @key;";
        }

        /// <summary>
        /// Select all tags with pagination.
        /// </summary>
        internal static string SelectAll()
        {
            return "SELECT * FROM tags ORDER BY document_id, key LIMIT @limit OFFSET @offset;";
        }

        /// <summary>
        /// Select distinct tag keys.
        /// </summary>
        internal static string SelectDistinctKeys()
        {
            return "SELECT DISTINCT key FROM tags ORDER BY key;";
        }

        /// <summary>
        /// Select documents with a specific tag key.
        /// </summary>
        internal static string SelectDocumentsByTagKey()
        {
            return @"
                SELECT d.*
                FROM documents d
                INNER JOIN tags t ON d.id = t.document_id
                WHERE t.key = @key
                ORDER BY d.indexed_utc DESC;
            ";
        }

        /// <summary>
        /// Select documents with a specific tag key-value pair.
        /// </summary>
        internal static string SelectDocumentsByTagKeyValue()
        {
            return @"
                SELECT d.*
                FROM documents d
                INNER JOIN tags t ON d.id = t.document_id
                WHERE t.key = @key AND t.value = @value
                ORDER BY d.indexed_utc DESC;
            ";
        }

        /// <summary>
        /// Select documents matching multiple tag filters (AND).
        /// </summary>
        /// <param name="filterCount">Number of key-value filters.</param>
        /// <returns>SQL string.</returns>
        internal static string SelectDocumentsWithAllTags(int filterCount)
        {
            string[] joins = new string[filterCount];
            string[] conditions = new string[filterCount];
            for (int i = 0; i < filterCount; i++)
            {
                joins[i] = $"INNER JOIN tags t{i} ON d.id = t{i}.document_id";
                conditions[i] = $"(t{i}.key = @key{i} AND t{i}.value = @value{i})";
            }
            return $@"
                SELECT d.*
                FROM documents d
                {string.Join(" ", joins)}
                WHERE {string.Join(" AND ", conditions)}
                ORDER BY d.indexed_utc DESC;
            ";
        }

        /// <summary>
        /// Check if a tag exists.
        /// </summary>
        internal static string Exists()
        {
            return @"
                SELECT 1 FROM tags
                WHERE (document_id = @documentId OR (document_id IS NULL AND @documentId IS NULL))
                AND key = @key
                LIMIT 1;
            ";
        }

        /// <summary>
        /// Check if an index-level tag exists.
        /// </summary>
        internal static string ExistsIndexTag()
        {
            return "SELECT 1 FROM tags WHERE document_id IS NULL AND key = @key LIMIT 1;";
        }

        /// <summary>
        /// Count tags for a document.
        /// </summary>
        internal static string CountByDocument()
        {
            return "SELECT COUNT(*) FROM tags WHERE document_id = @documentId;";
        }

        /// <summary>
        /// Count index-level tags.
        /// </summary>
        internal static string CountIndexTags()
        {
            return "SELECT COUNT(*) FROM tags WHERE document_id IS NULL;";
        }

        /// <summary>
        /// Update a tag value.
        /// </summary>
        internal static string Update()
        {
            return @"
                UPDATE tags SET value = @value, last_modified_utc = @lastModifiedUtc
                WHERE (document_id = @documentId OR (document_id IS NULL AND @documentId IS NULL))
                AND key = @key;
            ";
        }

        /// <summary>
        /// Delete a specific tag.
        /// </summary>
        internal static string DeleteByDocumentAndKey()
        {
            return @"
                DELETE FROM tags
                WHERE (document_id = @documentId OR (document_id IS NULL AND @documentId IS NULL))
                AND key = @key;
            ";
        }

        /// <summary>
        /// Delete all tags from a document.
        /// </summary>
        internal static string DeleteByDocumentId()
        {
            return "DELETE FROM tags WHERE document_id = @documentId;";
        }

        /// <summary>
        /// Delete an index-level tag.
        /// </summary>
        internal static string DeleteIndexTag()
        {
            return "DELETE FROM tags WHERE document_id IS NULL AND key = @key;";
        }

        /// <summary>
        /// Delete all index-level tags.
        /// </summary>
        internal static string DeleteAllIndexTags()
        {
            return "DELETE FROM tags WHERE document_id IS NULL;";
        }

        /// <summary>
        /// Delete all tags.
        /// </summary>
        internal static string DeleteAll()
        {
            return "DELETE FROM tags;";
        }

        /// <summary>
        /// Batch insert or replace tags for a document.
        /// </summary>
        /// <param name="count">Number of tags to insert.</param>
        /// <returns>SQL string.</returns>
        internal static string InsertOrReplaceBatch(int count)
        {
            List<string> values = new List<string>();
            for (int i = 0; i < count; i++)
            {
                values.Add($"(@id{i}, @documentId, @key{i}, @value{i}, @lastModifiedUtc, @createdUtc)");
            }
            return $@"
                INSERT INTO tags (id, document_id, key, value, last_modified_utc, created_utc)
                VALUES {string.Join(", ", values)}
                ON CONFLICT(document_id, key) DO UPDATE SET value = excluded.value, last_modified_utc = excluded.last_modified_utc;
            ";
        }
    }
}
