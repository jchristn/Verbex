namespace Verbex
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Threading;

    /// <summary>
    /// Thread-safe global term statistics for ranking algorithms such as TF-IDF and BM25
    /// </summary>
    public class TermStatistics : IDisposable
    {
        private long _TotalDocuments;
        private readonly ConcurrentDictionary<string, long> _TermDocumentFrequency;
        private readonly ConcurrentDictionary<string, long> _CollectionTermFrequency;
        private double _AverageDocumentLength;
        private long _TotalTermOccurrences;
        private readonly ReaderWriterLockSlim _Lock;

        /// <summary>
        /// Initializes a new instance of the TermStatistics class
        /// </summary>
        public TermStatistics()
        {
            _TotalDocuments = 0;
            _TermDocumentFrequency = new ConcurrentDictionary<string, long>();
            _CollectionTermFrequency = new ConcurrentDictionary<string, long>();
            _AverageDocumentLength = 0.0;
            _TotalTermOccurrences = 0;
            _Lock = new ReaderWriterLockSlim();
        }

        /// <summary>
        /// Gets the total number of documents in the collection
        /// Minimum value: 0
        /// </summary>
        public long TotalDocuments
        {
            get
            {
                _Lock.EnterReadLock();
                try
                {
                    return _TotalDocuments;
                }
                finally
                {
                    _Lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Gets the average document length in terms across all documents
        /// Minimum value: 0.0
        /// Used in BM25 scoring algorithm
        /// </summary>
        public double AverageDocumentLength
        {
            get
            {
                _Lock.EnterReadLock();
                try
                {
                    return _AverageDocumentLength;
                }
                finally
                {
                    _Lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Gets the total number of term occurrences across all documents
        /// Minimum value: 0
        /// </summary>
        public long TotalTermOccurrences
        {
            get
            {
                _Lock.EnterReadLock();
                try
                {
                    return _TotalTermOccurrences;
                }
                finally
                {
                    _Lock.ExitReadLock();
                }
            }
        }

        /// <summary>
        /// Gets the document frequency for a specific term (number of documents containing the term)
        /// </summary>
        /// <param name="term">The term to query</param>
        /// <returns>The document frequency, or 0 if term doesn't exist</returns>
        /// <exception cref="ArgumentNullException">Thrown when term is null</exception>
        public long GetDocumentFrequency(string term)
        {
            ArgumentNullException.ThrowIfNull(term);

            _TermDocumentFrequency.TryGetValue(term.ToLowerInvariant(), out long frequency);
            return frequency;
        }

        /// <summary>
        /// Gets the collection term frequency for a specific term (total occurrences across all documents)
        /// </summary>
        /// <param name="term">The term to query</param>
        /// <returns>The collection term frequency, or 0 if term doesn't exist</returns>
        /// <exception cref="ArgumentNullException">Thrown when term is null</exception>
        public long GetCollectionTermFrequency(string term)
        {
            ArgumentNullException.ThrowIfNull(term);

            _CollectionTermFrequency.TryGetValue(term.ToLowerInvariant(), out long frequency);
            return frequency;
        }

        /// <summary>
        /// Calculates the Inverse Document Frequency (IDF) for a term
        /// IDF = log(Total Documents / Document Frequency)
        /// </summary>
        /// <param name="term">The term to calculate IDF for</param>
        /// <returns>The IDF value, or 0.0 if term doesn't exist</returns>
        /// <exception cref="ArgumentNullException">Thrown when term is null</exception>
        public double CalculateIdf(string term)
        {
            ArgumentNullException.ThrowIfNull(term);

            _Lock.EnterReadLock();
            try
            {
                long documentFrequency = GetDocumentFrequency(term);
                if (documentFrequency == 0 || _TotalDocuments == 0)
                {
                    return 0.0;
                }

                return Math.Log((double)_TotalDocuments / documentFrequency);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Updates statistics when a document is added to the index
        /// </summary>
        /// <param name="documentLength">The length of the document in terms</param>
        /// <param name="termFrequencies">Dictionary of term frequencies in the document</param>
        /// <exception cref="ArgumentNullException">Thrown when termFrequencies is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when documentLength is negative</exception>
        public void AddDocument(long documentLength, Dictionary<string, int> termFrequencies)
        {
            ArgumentNullException.ThrowIfNull(termFrequencies);

            if (documentLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(documentLength), "Document length cannot be negative");
            }

            _Lock.EnterWriteLock();
            try
            {
                _TotalDocuments++;
                _TotalTermOccurrences += documentLength;

                foreach (KeyValuePair<string, int> termFreq in termFrequencies)
                {
                    string normalizedTerm = termFreq.Key.ToLowerInvariant();

                    _TermDocumentFrequency.AddOrUpdate(normalizedTerm, 1, (key, value) => value + 1);
                    _CollectionTermFrequency.AddOrUpdate(normalizedTerm, termFreq.Value, (key, value) => value + termFreq.Value);
                }

                RecalculateAverageDocumentLength();
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Updates statistics when a document is removed from the index
        /// </summary>
        /// <param name="documentLength">The length of the removed document in terms</param>
        /// <param name="termFrequencies">Dictionary of term frequencies in the removed document</param>
        /// <exception cref="ArgumentNullException">Thrown when termFrequencies is null</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when documentLength is negative</exception>
        /// <exception cref="InvalidOperationException">Thrown when trying to remove from empty collection</exception>
        public void RemoveDocument(long documentLength, Dictionary<string, int> termFrequencies)
        {
            ArgumentNullException.ThrowIfNull(termFrequencies);

            if (documentLength < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(documentLength), "Document length cannot be negative");
            }

            _Lock.EnterWriteLock();
            try
            {
                if (_TotalDocuments == 0)
                {
                    throw new InvalidOperationException("Cannot remove document from empty collection");
                }

                _TotalDocuments--;
                _TotalTermOccurrences -= documentLength;

                foreach (KeyValuePair<string, int> termFreq in termFrequencies)
                {
                    string normalizedTerm = termFreq.Key.ToLowerInvariant();

                    _TermDocumentFrequency.AddOrUpdate(normalizedTerm, 0, (key, value) => Math.Max(0, value - 1));
                    _CollectionTermFrequency.AddOrUpdate(normalizedTerm, 0, (key, value) => Math.Max(0, value - termFreq.Value));

                    if (_TermDocumentFrequency[normalizedTerm] == 0)
                    {
                        _TermDocumentFrequency.TryRemove(normalizedTerm, out long _);
                        _CollectionTermFrequency.TryRemove(normalizedTerm, out long _);
                    }
                }

                RecalculateAverageDocumentLength();
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Gets all terms currently tracked in the statistics
        /// </summary>
        /// <returns>Collection of all terms</returns>
        public IEnumerable<string> GetAllTerms()
        {
            _Lock.EnterReadLock();
            try
            {
                return new List<string>(_TermDocumentFrequency.Keys);
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Clears all statistics
        /// </summary>
        public void Clear()
        {
            _Lock.EnterWriteLock();
            try
            {
                _TotalDocuments = 0;
                _AverageDocumentLength = 0.0;
                _TotalTermOccurrences = 0;
                _TermDocumentFrequency.Clear();
                _CollectionTermFrequency.Clear();
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        private void RecalculateAverageDocumentLength()
        {
            if (_TotalDocuments > 0)
            {
                _AverageDocumentLength = (double)_TotalTermOccurrences / _TotalDocuments;
            }
            else
            {
                _AverageDocumentLength = 0.0;
            }
        }

        /// <summary>
        /// Releases all resources used by this TermStatistics instance
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases resources used by this TermStatistics instance
        /// </summary>
        /// <param name="disposing">True if disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _Lock?.Dispose();
            }
        }
    }
}