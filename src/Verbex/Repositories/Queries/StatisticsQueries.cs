namespace Verbex.Repositories.Queries
{
    /// <summary>
    /// Contains SQL queries for statistics and analytics operations.
    /// </summary>
    internal static class StatisticsQueries
    {
        /// <summary>
        /// Get overall index statistics.
        /// </summary>
        internal static string GetIndexStatistics()
        {
            return @"
                SELECT
                    (SELECT COUNT(*) FROM documents) as document_count,
                    (SELECT COUNT(*) FROM terms) as term_count,
                    (SELECT SUM(total_frequency) FROM terms) as total_term_occurrences,
                    (SELECT COUNT(*) FROM document_terms) as document_term_count,
                    (SELECT AVG(term_count) FROM documents) as avg_terms_per_document,
                    (SELECT AVG(document_frequency) FROM terms) as avg_document_frequency,
                    (SELECT MAX(document_frequency) FROM terms) as max_document_frequency,
                    (SELECT MIN(document_length) FROM documents) as min_document_length,
                    (SELECT MAX(document_length) FROM documents) as max_document_length,
                    (SELECT AVG(document_length) FROM documents) as avg_document_length;
            ";
        }

        /// <summary>
        /// Get statistics for a specific term.
        /// </summary>
        internal static string GetTermStatistics()
        {
            return @"
                SELECT
                    t.term,
                    t.document_frequency,
                    t.total_frequency,
                    t.created_utc,
                    t.last_updated_utc,
                    (SELECT COUNT(*) FROM documents) as total_documents,
                    AVG(dt.term_frequency) as avg_frequency_per_document,
                    MAX(dt.term_frequency) as max_frequency_in_document,
                    MIN(dt.term_frequency) as min_frequency_in_document
                FROM terms t
                LEFT JOIN document_terms dt ON t.id = dt.term_id
                WHERE t.term = @term
                GROUP BY t.id;
            ";
        }

        /// <summary>
        /// Get term statistics by ID.
        /// </summary>
        internal static string GetTermStatisticsById()
        {
            return @"
                SELECT
                    t.term,
                    t.document_frequency,
                    t.total_frequency,
                    t.created_utc,
                    t.last_updated_utc,
                    (SELECT COUNT(*) FROM documents) as total_documents,
                    AVG(dt.term_frequency) as avg_frequency_per_document,
                    MAX(dt.term_frequency) as max_frequency_in_document,
                    MIN(dt.term_frequency) as min_frequency_in_document
                FROM terms t
                LEFT JOIN document_terms dt ON t.id = dt.term_id
                WHERE t.id = @termId
                GROUP BY t.id;
            ";
        }

        /// <summary>
        /// Get top N terms by document frequency.
        /// </summary>
        internal static string GetTopTermsByDocumentFrequency()
        {
            return @"
                SELECT term, document_frequency, total_frequency
                FROM terms
                ORDER BY document_frequency DESC
                LIMIT @limit;
            ";
        }

        /// <summary>
        /// Get top N terms by total frequency.
        /// </summary>
        internal static string GetTopTermsByTotalFrequency()
        {
            return @"
                SELECT term, document_frequency, total_frequency
                FROM terms
                ORDER BY total_frequency DESC
                LIMIT @limit;
            ";
        }

        /// <summary>
        /// Get document statistics.
        /// </summary>
        internal static string GetDocumentStatistics()
        {
            return @"
                SELECT
                    d.id,
                    d.name,
                    d.document_length,
                    d.term_count,
                    d.indexed_utc,
                    COUNT(dt.id) as unique_terms,
                    SUM(dt.term_frequency) as total_term_occurrences,
                    (SELECT COUNT(*) FROM labels WHERE document_id = d.id) as label_count,
                    (SELECT COUNT(*) FROM tags WHERE document_id = d.id) as tag_count
                FROM documents d
                LEFT JOIN document_terms dt ON d.id = dt.document_id
                WHERE d.id = @documentId
                GROUP BY d.id;
            ";
        }

        /// <summary>
        /// Get term frequency distribution (histogram data).
        /// </summary>
        internal static string GetTermFrequencyDistribution()
        {
            return @"
                SELECT
                    CASE
                        WHEN document_frequency = 1 THEN '1'
                        WHEN document_frequency BETWEEN 2 AND 5 THEN '2-5'
                        WHEN document_frequency BETWEEN 6 AND 10 THEN '6-10'
                        WHEN document_frequency BETWEEN 11 AND 50 THEN '11-50'
                        WHEN document_frequency BETWEEN 51 AND 100 THEN '51-100'
                        ELSE '100+'
                    END as frequency_range,
                    COUNT(*) as term_count
                FROM terms
                GROUP BY frequency_range
                ORDER BY MIN(document_frequency);
            ";
        }

        /// <summary>
        /// Get document length distribution (histogram data).
        /// </summary>
        internal static string GetDocumentLengthDistribution()
        {
            return @"
                SELECT
                    CASE
                        WHEN document_length < 100 THEN '<100'
                        WHEN document_length BETWEEN 100 AND 499 THEN '100-499'
                        WHEN document_length BETWEEN 500 AND 999 THEN '500-999'
                        WHEN document_length BETWEEN 1000 AND 4999 THEN '1000-4999'
                        WHEN document_length BETWEEN 5000 AND 9999 THEN '5000-9999'
                        ELSE '10000+'
                    END as length_range,
                    COUNT(*) as document_count
                FROM documents
                GROUP BY length_range
                ORDER BY MIN(document_length);
            ";
        }

        /// <summary>
        /// Get label statistics.
        /// </summary>
        internal static string GetLabelStatistics()
        {
            return @"
                SELECT
                    label,
                    COUNT(CASE WHEN document_id IS NOT NULL THEN 1 END) as document_count,
                    COUNT(CASE WHEN document_id IS NULL THEN 1 END) as is_index_label
                FROM labels
                GROUP BY label
                ORDER BY document_count DESC;
            ";
        }

        /// <summary>
        /// Get tag key statistics.
        /// </summary>
        internal static string GetTagKeyStatistics()
        {
            return @"
                SELECT
                    key,
                    COUNT(DISTINCT value) as unique_values,
                    COUNT(CASE WHEN document_id IS NOT NULL THEN 1 END) as document_count,
                    COUNT(CASE WHEN document_id IS NULL THEN 1 END) as is_index_tag
                FROM tags
                GROUP BY key
                ORDER BY document_count DESC;
            ";
        }

        /// <summary>
        /// Get index metadata.
        /// </summary>
        internal static string GetIndexMetadata()
        {
            return "SELECT * FROM index_metadata LIMIT 1;";
        }

        /// <summary>
        /// Insert or update index metadata.
        /// </summary>
        internal static string UpsertIndexMetadata()
        {
            return @"
                INSERT INTO index_metadata (id, name, last_modified_utc, created_utc)
                VALUES (@id, @name, @lastModifiedUtc, @createdUtc)
                ON CONFLICT(id) DO UPDATE SET
                    name = @name,
                    last_modified_utc = @lastModifiedUtc;
            ";
        }

        /// <summary>
        /// Update last modified timestamp.
        /// </summary>
        internal static string UpdateLastModified()
        {
            return "UPDATE index_metadata SET last_modified_utc = @lastModifiedUtc;";
        }
    }
}
