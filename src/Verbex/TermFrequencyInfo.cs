namespace Verbex
{
    using System;

    /// <summary>
    /// Information about term frequency
    /// </summary>
    public class TermFrequencyInfo
    {
        /// <summary>
        /// Gets the term
        /// </summary>
        public string Term { get; init; }

        /// <summary>
        /// Gets the document frequency (number of documents containing this term)
        /// </summary>
        public long DocumentFrequency { get; init; }

        /// <summary>
        /// Gets the collection frequency (total occurrences across all documents)
        /// </summary>
        public long CollectionFrequency { get; init; }

        /// <summary>
        /// Initializes a new instance of the TermFrequencyInfo class
        /// </summary>
        /// <param name="term">The term</param>
        /// <param name="documentFrequency">Document frequency</param>
        /// <param name="collectionFrequency">Collection frequency</param>
        /// <exception cref="ArgumentNullException">Thrown when term is null</exception>
        public TermFrequencyInfo(string term, long documentFrequency, long collectionFrequency)
        {
            ArgumentNullException.ThrowIfNull(term);
            Term = term;
            DocumentFrequency = documentFrequency;
            CollectionFrequency = collectionFrequency;
        }
    }
}
