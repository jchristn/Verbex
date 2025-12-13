namespace Test
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Verbex;

    /// <summary>
    /// Helper methods for test operations.
    /// </summary>
    public static class TestHelpers
    {
        /// <summary>
        /// Adds a document to the index.
        /// </summary>
        /// <param name="index">The inverted index.</param>
        /// <param name="documentName">Document name.</param>
        /// <param name="content">Document content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Document ID.</returns>
        public static async Task<string> AddTestDocumentAsync(
            InvertedIndex index,
            string documentName,
            string content,
            CancellationToken cancellationToken = default)
        {
            return await index.AddDocumentAsync(documentName, content, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Adds a document with a specific ID to the index.
        /// </summary>
        /// <param name="index">The inverted index.</param>
        /// <param name="docId">Document ID.</param>
        /// <param name="documentName">Document name.</param>
        /// <param name="content">Document content.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task AddTestDocumentAsync(
            InvertedIndex index,
            string docId,
            string documentName,
            string content,
            CancellationToken cancellationToken = default)
        {
            await index.AddDocumentAsync(docId, documentName, content, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Checks if a document exists.
        /// </summary>
        /// <param name="index">The inverted index.</param>
        /// <param name="docId">Document ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if document exists, false otherwise.</returns>
        public static async Task<bool> DocumentExistsAsync(
            InvertedIndex index,
            string docId,
            CancellationToken cancellationToken = default)
        {
            return await index.DocumentExistsAsync(docId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Removes a document from the index.
        /// </summary>
        /// <param name="index">The inverted index.</param>
        /// <param name="docId">Document ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>True if document was removed, false if it didn't exist.</returns>
        public static async Task<bool> RemoveTestDocumentAsync(
            InvertedIndex index,
            string docId,
            CancellationToken cancellationToken = default)
        {
            return await index.RemoveDocumentAsync(docId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets document metadata.
        /// </summary>
        /// <param name="index">The inverted index.</param>
        /// <param name="docId">Document ID.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Document metadata if found, null otherwise.</returns>
        public static async Task<DocumentMetadata?> GetDocumentMetadataAsync(
            InvertedIndex index,
            string docId,
            CancellationToken cancellationToken = default)
        {
            return await index.GetDocumentAsync(docId, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Displays search results with detailed scoring information.
        /// </summary>
        /// <param name="results">Search results to display.</param>
        /// <param name="query">The search query that was executed.</param>
        /// <param name="testName">Name of the test for context.</param>
        public static void DisplaySearchResults(SearchResults results, string query, string testName = "Search Test")
        {
            Console.WriteLine($"--- {testName} ---");
            Console.WriteLine($"Query: \"{query}\"");
            Console.WriteLine($"Results found: {results.TotalCount}");
            Console.WriteLine($"Search time: {results.SearchTime.TotalMilliseconds:F2}ms");

            if (results.TotalCount == 0)
            {
                Console.WriteLine("No results found.");
                Console.WriteLine();
                return;
            }

            Console.WriteLine();

            for (int i = 0; i < results.Results.Count; i++)
            {
                SearchResult result = results.Results[i];
                Console.WriteLine($"Result {i + 1}:");
                Console.WriteLine($"  Document ID: {result.DocumentId}");
                if (result.Document != null)
                {
                    Console.WriteLine($"  Document Name: {result.Document.DocumentPath}");
                }
                Console.WriteLine($"  Score: {result.Score:F4}");
                Console.WriteLine($"  Matched Terms: {result.MatchedTermCount}");
                Console.WriteLine();
            }
        }
    }
}
