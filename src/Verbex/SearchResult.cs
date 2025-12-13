namespace Verbex
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Represents a search result with relevance scoring.
    /// </summary>
    public class SearchResult
    {
        private string _DocumentId;
        private DocumentMetadata? _Document;
        private double _Score;
        private int _MatchedTermCount;
        private Dictionary<string, double> _TermScores;
        private Dictionary<string, int> _TermFrequencies;
        private int _TotalTermMatches;

        /// <summary>
        /// Initializes a new instance of the SearchResult class.
        /// </summary>
        /// <param name="documentId">The document identifier.</param>
        /// <param name="score">The relevance score.</param>
        /// <exception cref="ArgumentException">Thrown when documentId is empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when score is negative.</exception>
        public SearchResult(string documentId, double score)
        {
            if (string.IsNullOrEmpty(documentId))
            {
                throw new ArgumentException("Document ID cannot be empty.", nameof(documentId));
            }

            if (score < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(score), "Score cannot be negative.");
            }

            _DocumentId = documentId;
            _Document = null;
            _Score = score;
            _MatchedTermCount = 0;
            _TermScores = new Dictionary<string, double>();
            _TermFrequencies = new Dictionary<string, int>();
            _TotalTermMatches = 0;
        }

        /// <summary>
        /// Initializes a new instance of the SearchResult class with document metadata.
        /// </summary>
        /// <param name="document">The document metadata.</param>
        /// <param name="score">The relevance score.</param>
        /// <param name="matchedTermCount">Number of query terms matched.</param>
        /// <exception cref="ArgumentNullException">Thrown when document is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when score is negative or matchedTermCount is negative.</exception>
        public SearchResult(DocumentMetadata document, double score, int matchedTermCount)
        {
            ArgumentNullException.ThrowIfNull(document);

            if (score < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(score), "Score cannot be negative.");
            }

            if (matchedTermCount < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(matchedTermCount), "Matched term count cannot be negative.");
            }

            _DocumentId = document.DocumentId;
            _Document = document;
            _Score = score;
            _MatchedTermCount = matchedTermCount;
            _TermScores = new Dictionary<string, double>();
            _TermFrequencies = new Dictionary<string, int>();
            _TotalTermMatches = 0;
        }

        /// <summary>
        /// Gets the document identifier.
        /// </summary>
        public string DocumentId
        {
            get { return _DocumentId; }
        }

        /// <summary>
        /// Gets the document metadata, if available.
        /// </summary>
        public DocumentMetadata? Document
        {
            get { return _Document; }
        }

        /// <summary>
        /// Gets the number of query terms that matched this document.
        /// </summary>
        public int MatchedTermCount
        {
            get { return _MatchedTermCount; }
        }

        /// <summary>
        /// Gets or sets the overall relevance score for this result.
        /// Minimum value: 0.0
        /// Higher scores indicate greater relevance
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when value is negative</exception>
        public double Score
        {
            get { return _Score; }
            set
            {
                if (value < 0.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "Score cannot be negative");
                }
                _Score = value;
            }
        }

        /// <summary>
        /// Gets a read-only dictionary of term-specific scores
        /// Maps each query term to its contribution to the overall score
        /// </summary>
        public IReadOnlyDictionary<string, double> TermScores
        {
            get { return _TermScores; }
        }

        /// <summary>
        /// Gets a read-only dictionary of term frequencies in the document
        /// Maps each query term to its frequency in the matched document
        /// </summary>
        public IReadOnlyDictionary<string, int> TermFrequencies
        {
            get { return _TermFrequencies; }
        }

        /// <summary>
        /// Gets the total number of term matches across all query terms
        /// Minimum value: 0
        /// </summary>
        public int TotalTermMatches
        {
            get { return _TotalTermMatches; }
        }

        /// <summary>
        /// Adds or updates the score for a specific term
        /// </summary>
        /// <param name="term">The term</param>
        /// <param name="score">The term's contribution to the overall score</param>
        /// <param name="frequency">The frequency of the term in the document</param>
        /// <exception cref="ArgumentNullException">Thrown when term is null</exception>
        /// <exception cref="ArgumentException">Thrown when term is empty or whitespace</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when score is negative or frequency is negative</exception>
        public void AddTermScore(string term, double score, int frequency)
        {
            ArgumentNullException.ThrowIfNull(term);

            if (string.IsNullOrWhiteSpace(term))
            {
                throw new ArgumentException("Term cannot be empty or whitespace", nameof(term));
            }

            if (score < 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(score), "Score cannot be negative");
            }

            if (frequency < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency cannot be negative");
            }

            string normalizedTerm = term.ToLowerInvariant();

            if (_TermScores.ContainsKey(normalizedTerm))
            {
                _TotalTermMatches -= _TermFrequencies[normalizedTerm];
            }

            _TermScores[normalizedTerm] = score;
            _TermFrequencies[normalizedTerm] = frequency;
            _TotalTermMatches += frequency;
        }

        /// <summary>
        /// Gets the score for a specific term
        /// </summary>
        /// <param name="term">The term to get the score for</param>
        /// <returns>The term score, or 0.0 if term not found</returns>
        /// <exception cref="ArgumentNullException">Thrown when term is null</exception>
        public double GetTermScore(string term)
        {
            ArgumentNullException.ThrowIfNull(term);

            string normalizedTerm = term.ToLowerInvariant();
            _TermScores.TryGetValue(normalizedTerm, out double score);
            return score;
        }

        /// <summary>
        /// Gets the frequency for a specific term
        /// </summary>
        /// <param name="term">The term to get the frequency for</param>
        /// <returns>The term frequency, or 0 if term not found</returns>
        /// <exception cref="ArgumentNullException">Thrown when term is null</exception>
        public int GetTermFrequency(string term)
        {
            ArgumentNullException.ThrowIfNull(term);

            string normalizedTerm = term.ToLowerInvariant();
            _TermFrequencies.TryGetValue(normalizedTerm, out int frequency);
            return frequency;
        }

        /// <summary>
        /// Checks if this result contains a specific term
        /// </summary>
        /// <param name="term">The term to check for</param>
        /// <returns>True if the term is present, false otherwise</returns>
        /// <exception cref="ArgumentNullException">Thrown when term is null</exception>
        public bool ContainsTerm(string term)
        {
            ArgumentNullException.ThrowIfNull(term);

            string normalizedTerm = term.ToLowerInvariant();
            return _TermScores.ContainsKey(normalizedTerm);
        }

        /// <summary>
        /// Gets all query terms that matched in this document
        /// </summary>
        /// <returns>Collection of matched terms</returns>
        public IEnumerable<string> GetMatchedTerms()
        {
            return new List<string>(_TermScores.Keys);
        }

        /// <summary>
        /// Calculates the percentage of query terms that matched in this document
        /// </summary>
        /// <param name="totalQueryTerms">Total number of terms in the original query</param>
        /// <returns>Match percentage between 0.0 and 1.0</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when totalQueryTerms is less than 1</exception>
        public double CalculateTermMatchPercentage(int totalQueryTerms)
        {
            if (totalQueryTerms < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(totalQueryTerms), "Total query terms must be at least 1");
            }

            return (double)_TermScores.Count / totalQueryTerms;
        }
    }
}