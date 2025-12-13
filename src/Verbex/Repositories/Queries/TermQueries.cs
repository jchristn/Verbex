namespace Verbex.Repositories.Queries
{
    /// <summary>
    /// Contains SQL queries for term operations.
    /// </summary>
    internal static class TermQueries
    {
        /// <summary>
        /// Insert a new term.
        /// </summary>
        internal static string Insert()
        {
            return @"
                INSERT INTO terms (id, term, document_frequency, total_frequency, last_updated_utc, created_utc)
                VALUES (@id, @term, @documentFrequency, @totalFrequency, @lastUpdatedUtc, @createdUtc);
            ";
        }

        /// <summary>
        /// Insert a term or ignore if exists, returning the ID.
        /// </summary>
        internal static string InsertOrIgnore()
        {
            return @"
                INSERT OR IGNORE INTO terms (id, term, document_frequency, total_frequency, last_updated_utc, created_utc)
                VALUES (@id, @term, 0, 0, @lastUpdatedUtc, @createdUtc);
            ";
        }

        /// <summary>
        /// Select a term by ID.
        /// </summary>
        internal static string SelectById()
        {
            return "SELECT * FROM terms WHERE id = @id;";
        }

        /// <summary>
        /// Select a term by term text.
        /// </summary>
        internal static string SelectByTerm()
        {
            return "SELECT * FROM terms WHERE term = @term;";
        }

        /// <summary>
        /// Select terms by term texts.
        /// </summary>
        /// <param name="count">Number of terms to select.</param>
        /// <returns>SQL string with parameterized IN clause.</returns>
        internal static string SelectByTerms(int count)
        {
            string[] parameters = new string[count];
            for (int i = 0; i < count; i++)
            {
                parameters[i] = $"@term{i}";
            }
            return $"SELECT * FROM terms WHERE term IN ({string.Join(", ", parameters)});";
        }

        /// <summary>
        /// Select all terms with pagination.
        /// </summary>
        internal static string SelectAll()
        {
            return "SELECT * FROM terms ORDER BY document_frequency DESC LIMIT @limit OFFSET @offset;";
        }

        /// <summary>
        /// Select terms matching a prefix pattern.
        /// </summary>
        internal static string SelectByPrefix()
        {
            return "SELECT * FROM terms WHERE term LIKE @pattern ORDER BY document_frequency DESC LIMIT @limit;";
        }

        /// <summary>
        /// Count total terms.
        /// </summary>
        internal static string Count()
        {
            return "SELECT COUNT(*) FROM terms;";
        }

        /// <summary>
        /// Check if term exists.
        /// </summary>
        internal static string Exists()
        {
            return "SELECT 1 FROM terms WHERE term = @term LIMIT 1;";
        }

        /// <summary>
        /// Update term frequencies.
        /// </summary>
        internal static string UpdateFrequencies()
        {
            return @"
                UPDATE terms
                SET document_frequency = @documentFrequency,
                    total_frequency = @totalFrequency,
                    last_updated_utc = @lastUpdatedUtc
                WHERE id = @id;
            ";
        }

        /// <summary>
        /// Increment term frequencies.
        /// </summary>
        internal static string IncrementFrequencies()
        {
            return @"
                UPDATE terms
                SET document_frequency = document_frequency + @documentFrequencyDelta,
                    total_frequency = total_frequency + @totalFrequencyDelta,
                    last_updated_utc = @lastUpdatedUtc
                WHERE id = @id;
            ";
        }

        /// <summary>
        /// Decrement term frequencies.
        /// </summary>
        internal static string DecrementFrequencies()
        {
            return @"
                UPDATE terms
                SET document_frequency = document_frequency - @documentFrequencyDelta,
                    total_frequency = total_frequency - @totalFrequencyDelta,
                    last_updated_utc = @lastUpdatedUtc
                WHERE id = @id;
            ";
        }

        /// <summary>
        /// Delete a term by ID.
        /// </summary>
        internal static string DeleteById()
        {
            return "DELETE FROM terms WHERE id = @id;";
        }

        /// <summary>
        /// Delete a term by term text.
        /// </summary>
        internal static string DeleteByTerm()
        {
            return "DELETE FROM terms WHERE term = @term;";
        }

        /// <summary>
        /// Delete terms with zero document frequency (cleanup).
        /// </summary>
        internal static string DeleteOrphaned()
        {
            return "DELETE FROM terms WHERE document_frequency <= 0;";
        }

        /// <summary>
        /// Delete all terms.
        /// </summary>
        internal static string DeleteAll()
        {
            return "DELETE FROM terms;";
        }

        /// <summary>
        /// Get total term frequency sum (for statistics).
        /// </summary>
        internal static string GetTotalFrequencySum()
        {
            return "SELECT SUM(total_frequency) FROM terms;";
        }

        /// <summary>
        /// Get top N terms by document frequency.
        /// </summary>
        internal static string GetTopTerms()
        {
            return "SELECT * FROM terms ORDER BY document_frequency DESC LIMIT @limit;";
        }

        /// <summary>
        /// Batch insert terms, ignoring duplicates.
        /// </summary>
        /// <param name="count">Number of terms to insert.</param>
        /// <returns>SQL string.</returns>
        internal static string InsertBatchIgnore(int count)
        {
            List<string> values = new List<string>();
            for (int i = 0; i < count; i++)
            {
                values.Add($"(@id{i}, @term{i}, 0, 0, @lastUpdatedUtc, @createdUtc)");
            }
            return $@"
                INSERT OR IGNORE INTO terms (id, term, document_frequency, total_frequency, last_updated_utc, created_utc)
                VALUES {string.Join(", ", values)};
            ";
        }

        /// <summary>
        /// Batch increment term frequencies using CASE expressions.
        /// </summary>
        /// <param name="count">Number of terms to update.</param>
        /// <returns>SQL string.</returns>
        internal static string IncrementFrequenciesBatch(int count)
        {
            string[] idParams = new string[count];
            List<string> docFreqCases = new List<string>();
            List<string> totalFreqCases = new List<string>();

            for (int i = 0; i < count; i++)
            {
                idParams[i] = $"@id{i}";
                docFreqCases.Add($"WHEN @id{i} THEN @docFreqDelta{i}");
                totalFreqCases.Add($"WHEN @id{i} THEN @totalFreqDelta{i}");
            }

            return $@"
                UPDATE terms
                SET document_frequency = document_frequency + CASE id {string.Join(" ", docFreqCases)} ELSE 0 END,
                    total_frequency = total_frequency + CASE id {string.Join(" ", totalFreqCases)} ELSE 0 END,
                    last_updated_utc = @lastUpdatedUtc
                WHERE id IN ({string.Join(", ", idParams)});
            ";
        }

        /// <summary>
        /// Batch decrement term frequencies using CASE expressions.
        /// </summary>
        /// <param name="count">Number of terms to update.</param>
        /// <returns>SQL string.</returns>
        internal static string DecrementFrequenciesBatch(int count)
        {
            string[] idParams = new string[count];
            List<string> docFreqCases = new List<string>();
            List<string> totalFreqCases = new List<string>();

            for (int i = 0; i < count; i++)
            {
                idParams[i] = $"@id{i}";
                docFreqCases.Add($"WHEN @id{i} THEN @docFreqDelta{i}");
                totalFreqCases.Add($"WHEN @id{i} THEN @totalFreqDelta{i}");
            }

            return $@"
                UPDATE terms
                SET document_frequency = document_frequency - CASE id {string.Join(" ", docFreqCases)} ELSE 0 END,
                    total_frequency = total_frequency - CASE id {string.Join(" ", totalFreqCases)} ELSE 0 END,
                    last_updated_utc = @lastUpdatedUtc
                WHERE id IN ({string.Join(", ", idParams)});
            ";
        }
    }
}
