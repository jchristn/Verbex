namespace Verbex
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Basic stop word remover implementation using a predefined list of common English stop words.
    /// This implementation focuses on the most frequently used stop words that typically don't contribute
    /// meaningful content to search operations.
    /// Thread-safe for concurrent access.
    /// </summary>
    public class BasicStopWordRemover : IStopWordRemover
    {
        private static readonly HashSet<string> _StopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // Articles
            "a", "an", "the",

            // Prepositions
            "at", "by", "for", "from", "in", "into", "of", "on", "to", "with", "without",
            "about", "above", "across", "after", "against", "along", "among", "around",
            "before", "behind", "below", "beneath", "beside", "between", "beyond",
            "during", "except", "inside", "near", "over", "through", "under", "until",

            // Pronouns
            "i", "me", "my", "myself", "we", "our", "ours", "ourselves", "you", "your",
            "yours", "yourself", "yourselves", "he", "him", "his", "himself", "she",
            "her", "hers", "herself", "it", "its", "itself", "they", "them", "their",
            "theirs", "themselves", "what", "which", "who", "whom", "this", "that",
            "these", "those",

            // Auxiliary verbs
            "am", "is", "are", "was", "were", "be", "been", "being", "have", "has",
            "had", "do", "does", "did", "will", "would", "could", "should", "may",
            "might", "must", "can", "shall",

            // Common conjunctions and connectors
            "and", "or", "but", "if", "then", "else", "when", "where", "why", "how",
            "because", "since", "although", "though", "while", "whereas", "unless",
            "until", "as", "so", "than", "that", "whether",

            // Common adverbs
            "not", "no", "nor", "only", "just", "also", "too", "very", "much", "many",
            "more", "most", "less", "least", "quite", "rather", "really", "still",
            "yet", "already", "again", "once", "twice", "always", "never", "often",
            "sometimes", "usually", "here", "there", "where", "everywhere", "anywhere",
            "somewhere", "nowhere", "now", "then", "today", "yesterday", "tomorrow",

            // Other common words
            "all", "any", "both", "each", "few", "other", "another", "such", "own",
            "same", "so", "than", "too", "s", "t", "can", "don", "should", "now"
        };

        /// <summary>
        /// Determines whether a word should be removed as a stop word.
        /// </summary>
        /// <param name="word">The word to evaluate. Cannot be null or empty.</param>
        /// <returns>True if the word is a stop word and should be removed; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when word is null.</exception>
        /// <exception cref="ArgumentException">Thrown when word is empty or whitespace.</exception>
        public bool IsStopWord(string word)
        {
            ArgumentNullException.ThrowIfNull(word);
            if (string.IsNullOrWhiteSpace(word))
                throw new ArgumentException("Word cannot be empty or whitespace.", nameof(word));

            return _StopWords.Contains(word.Trim());
        }
    }
}