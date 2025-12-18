namespace Verbex.Repositories.Queries
{
    /// <summary>
    /// Contains SQL queries for document-term mapping operations.
    /// </summary>
    internal static class DocumentTermQueries
    {
        /// <summary>
        /// Insert a document-term mapping.
        /// </summary>
        internal static string Insert()
        {
            return @"
                INSERT INTO document_terms (id, document_id, term_id, term_frequency, character_positions, term_positions, last_modified_utc, created_utc)
                VALUES (@id, @documentId, @termId, @termFrequency, @characterPositions, @termPositions, @lastModifiedUtc, @createdUtc);
            ";
        }

        /// <summary>
        /// Insert or replace a document-term mapping.
        /// </summary>
        internal static string InsertOrReplace()
        {
            return @"
                INSERT OR REPLACE INTO document_terms (id, document_id, term_id, term_frequency, character_positions, term_positions, last_modified_utc, created_utc)
                VALUES (@id, @documentId, @termId, @termFrequency, @characterPositions, @termPositions, @lastModifiedUtc, @createdUtc);
            ";
        }

        /// <summary>
        /// Select mappings by document ID.
        /// </summary>
        internal static string SelectByDocumentId()
        {
            return @"
                SELECT dt.*, t.term
                FROM document_terms dt
                INNER JOIN terms t ON dt.term_id = t.id
                WHERE dt.document_id = @documentId;
            ";
        }

        /// <summary>
        /// Select mappings by term ID.
        /// </summary>
        internal static string SelectByTermId()
        {
            return @"
                SELECT dt.*, d.name as document_name
                FROM document_terms dt
                INNER JOIN documents d ON dt.document_id = d.id
                WHERE dt.term_id = @termId
                ORDER BY dt.term_frequency DESC;
            ";
        }

        /// <summary>
        /// Select mappings by term text.
        /// </summary>
        internal static string SelectByTerm()
        {
            return @"
                SELECT dt.*, d.name as document_name
                FROM document_terms dt
                INNER JOIN terms t ON dt.term_id = t.id
                INNER JOIN documents d ON dt.document_id = d.id
                WHERE t.term = @term
                ORDER BY dt.term_frequency DESC;
            ";
        }

        /// <summary>
        /// Select mappings by multiple term IDs.
        /// </summary>
        /// <param name="count">Number of term IDs.</param>
        /// <returns>SQL string with parameterized IN clause.</returns>
        internal static string SelectByTermIds(int count)
        {
            string[] parameters = new string[count];
            for (int i = 0; i < count; i++)
            {
                parameters[i] = $"@termId{i}";
            }
            return $@"
                SELECT dt.*, d.name as document_name, t.term
                FROM document_terms dt
                INNER JOIN documents d ON dt.document_id = d.id
                INNER JOIN terms t ON dt.term_id = t.id
                WHERE dt.term_id IN ({string.Join(", ", parameters)})
                ORDER BY dt.document_id, dt.term_frequency DESC;
            ";
        }

        /// <summary>
        /// Select a specific document-term mapping.
        /// </summary>
        internal static string SelectByDocumentAndTerm()
        {
            return @"
                SELECT dt.*, t.term
                FROM document_terms dt
                INNER JOIN terms t ON dt.term_id = t.id
                WHERE dt.document_id = @documentId AND dt.term_id = @termId;
            ";
        }

        /// <summary>
        /// Check if a document-term mapping exists.
        /// </summary>
        internal static string Exists()
        {
            return "SELECT 1 FROM document_terms WHERE document_id = @documentId AND term_id = @termId LIMIT 1;";
        }

        /// <summary>
        /// Count mappings for a document.
        /// </summary>
        internal static string CountByDocument()
        {
            return "SELECT COUNT(*) FROM document_terms WHERE document_id = @documentId;";
        }

        /// <summary>
        /// Count mappings for a term (document frequency).
        /// </summary>
        internal static string CountByTerm()
        {
            return "SELECT COUNT(*) FROM document_terms WHERE term_id = @termId;";
        }

        /// <summary>
        /// Sum term frequency for a term (total frequency).
        /// </summary>
        internal static string SumFrequencyByTerm()
        {
            return "SELECT SUM(term_frequency) FROM document_terms WHERE term_id = @termId;";
        }

        /// <summary>
        /// Update a document-term mapping.
        /// </summary>
        internal static string Update()
        {
            return @"
                UPDATE document_terms
                SET term_frequency = @termFrequency,
                    character_positions = @characterPositions,
                    term_positions = @termPositions,
                    last_modified_utc = @lastModifiedUtc
                WHERE document_id = @documentId AND term_id = @termId;
            ";
        }

        /// <summary>
        /// Delete mappings by document ID.
        /// </summary>
        internal static string DeleteByDocumentId()
        {
            return "DELETE FROM document_terms WHERE document_id = @documentId;";
        }

        /// <summary>
        /// Delete mappings by term ID.
        /// </summary>
        internal static string DeleteByTermId()
        {
            return "DELETE FROM document_terms WHERE term_id = @termId;";
        }

        /// <summary>
        /// Delete a specific mapping.
        /// </summary>
        internal static string DeleteByDocumentAndTerm()
        {
            return "DELETE FROM document_terms WHERE document_id = @documentId AND term_id = @termId;";
        }

        /// <summary>
        /// Delete all mappings.
        /// </summary>
        internal static string DeleteAll()
        {
            return "DELETE FROM document_terms;";
        }

        /// <summary>
        /// Get documents containing all specified terms (AND search).
        /// </summary>
        /// <param name="termCount">Number of terms to match.</param>
        /// <returns>SQL string.</returns>
        internal static string SelectDocumentsWithAllTerms(int termCount)
        {
            string[] parameters = new string[termCount];
            for (int i = 0; i < termCount; i++)
            {
                parameters[i] = $"@termId{i}";
            }
            return $@"
                SELECT dt.document_id, SUM(dt.term_frequency) as total_frequency, COUNT(DISTINCT dt.term_id) as matched_terms
                FROM document_terms dt
                WHERE dt.term_id IN ({string.Join(", ", parameters)})
                GROUP BY dt.document_id
                HAVING COUNT(DISTINCT dt.term_id) = {termCount}
                ORDER BY total_frequency DESC
                LIMIT @limit;
            ";
        }

        /// <summary>
        /// Get documents containing any of the specified terms (OR search).
        /// </summary>
        /// <param name="termCount">Number of terms to match.</param>
        /// <returns>SQL string.</returns>
        internal static string SelectDocumentsWithAnyTerms(int termCount)
        {
            string[] parameters = new string[termCount];
            for (int i = 0; i < termCount; i++)
            {
                parameters[i] = $"@termId{i}";
            }
            return $@"
                SELECT dt.document_id, SUM(dt.term_frequency) as total_frequency, COUNT(DISTINCT dt.term_id) as matched_terms
                FROM document_terms dt
                WHERE dt.term_id IN ({string.Join(", ", parameters)})
                GROUP BY dt.document_id
                ORDER BY matched_terms DESC, total_frequency DESC
                LIMIT @limit;
            ";
        }

        /// <summary>
        /// Get documents containing all specified terms (AND search) with label filtering.
        /// </summary>
        /// <param name="termCount">Number of terms to match.</param>
        /// <param name="labelCount">Number of labels to filter by.</param>
        /// <param name="tagCount">Number of tags to filter by.</param>
        /// <returns>SQL string.</returns>
        internal static string SelectDocumentsWithAllTermsFiltered(int termCount, int labelCount, int tagCount)
        {
            string[] termParams = new string[termCount];
            for (int i = 0; i < termCount; i++)
            {
                termParams[i] = $"@termId{i}";
            }

            string labelJoin = "";
            if (labelCount > 0)
            {
                string[] labelParams = new string[labelCount];
                for (int i = 0; i < labelCount; i++)
                {
                    labelParams[i] = $"@label{i}";
                }
                labelJoin = $@"
                    INNER JOIN (
                        SELECT document_id
                        FROM labels
                        WHERE label IN ({string.Join(", ", labelParams)})
                        GROUP BY document_id
                        HAVING COUNT(DISTINCT label) = {labelCount}
                    ) lf ON dt.document_id = lf.document_id";
            }

            string tagJoin = "";
            if (tagCount > 0)
            {
                for (int i = 0; i < tagCount; i++)
                {
                    tagJoin += $@"
                    INNER JOIN tags t{i} ON dt.document_id = t{i}.document_id
                        AND t{i}.key = @tagKey{i} AND t{i}.value = @tagValue{i}";
                }
            }

            return $@"
                SELECT dt.document_id, SUM(dt.term_frequency) as total_frequency, COUNT(DISTINCT dt.term_id) as matched_terms
                FROM document_terms dt
                {labelJoin}
                {tagJoin}
                WHERE dt.term_id IN ({string.Join(", ", termParams)})
                GROUP BY dt.document_id
                HAVING COUNT(DISTINCT dt.term_id) = {termCount}
                ORDER BY total_frequency DESC
                LIMIT @limit;
            ";
        }

        /// <summary>
        /// Batch insert document-term mappings.
        /// </summary>
        /// <param name="count">Number of mappings to insert.</param>
        /// <returns>SQL string.</returns>
        internal static string InsertBatch(int count)
        {
            List<string> values = new List<string>();
            for (int i = 0; i < count; i++)
            {
                values.Add($"(@id{i}, @documentId, @termId{i}, @termFrequency{i}, @characterPositions{i}, @termPositions{i}, @lastModifiedUtc, @createdUtc)");
            }
            return $@"
                INSERT INTO document_terms (id, document_id, term_id, term_frequency, character_positions, term_positions, last_modified_utc, created_utc)
                VALUES {string.Join(", ", values)};
            ";
        }

        /// <summary>
        /// Get documents containing any of the specified terms (OR search) with label/tag filtering.
        /// </summary>
        /// <param name="termCount">Number of terms to match.</param>
        /// <param name="labelCount">Number of labels to filter by.</param>
        /// <param name="tagCount">Number of tags to filter by.</param>
        /// <returns>SQL string.</returns>
        internal static string SelectDocumentsWithAnyTermsFiltered(int termCount, int labelCount, int tagCount)
        {
            string[] termParams = new string[termCount];
            for (int i = 0; i < termCount; i++)
            {
                termParams[i] = $"@termId{i}";
            }

            string labelJoin = "";
            if (labelCount > 0)
            {
                string[] labelParams = new string[labelCount];
                for (int i = 0; i < labelCount; i++)
                {
                    labelParams[i] = $"@label{i}";
                }
                labelJoin = $@"
                    INNER JOIN (
                        SELECT document_id
                        FROM labels
                        WHERE label IN ({string.Join(", ", labelParams)})
                        GROUP BY document_id
                        HAVING COUNT(DISTINCT label) = {labelCount}
                    ) lf ON dt.document_id = lf.document_id";
            }

            string tagJoin = "";
            if (tagCount > 0)
            {
                for (int i = 0; i < tagCount; i++)
                {
                    tagJoin += $@"
                    INNER JOIN tags t{i} ON dt.document_id = t{i}.document_id
                        AND t{i}.key = @tagKey{i} AND t{i}.value = @tagValue{i}";
                }
            }

            return $@"
                SELECT dt.document_id, SUM(dt.term_frequency) as total_frequency, COUNT(DISTINCT dt.term_id) as matched_terms
                FROM document_terms dt
                {labelJoin}
                {tagJoin}
                WHERE dt.term_id IN ({string.Join(", ", termParams)})
                GROUP BY dt.document_id
                ORDER BY matched_terms DESC, total_frequency DESC
                LIMIT @limit;
            ";
        }

        /// <summary>
        /// Select document-term records for specific documents and terms.
        /// </summary>
        /// <param name="documentCount">Number of document IDs.</param>
        /// <param name="termCount">Number of term IDs.</param>
        /// <returns>SQL string.</returns>
        internal static string SelectByDocumentsAndTerms(int documentCount, int termCount)
        {
            string[] docParams = new string[documentCount];
            for (int i = 0; i < documentCount; i++)
            {
                docParams[i] = $"@docId{i}";
            }

            string[] termParams = new string[termCount];
            for (int i = 0; i < termCount; i++)
            {
                termParams[i] = $"@termId{i}";
            }

            return $@"
                SELECT document_id, term_id, term_frequency
                FROM document_terms
                WHERE document_id IN ({string.Join(", ", docParams)})
                  AND term_id IN ({string.Join(", ", termParams)});
            ";
        }
    }
}
