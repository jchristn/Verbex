namespace Verbex
{
    using System;

    /// <summary>
    /// Interface for removing common stop words from text processing.
    /// Stop words are frequently used words that typically don't contribute significant meaning to search queries.
    /// </summary>
    public interface IStopWordRemover
    {
        /// <summary>
        /// Determines whether a word should be removed as a stop word.
        /// </summary>
        /// <param name="word">The word to evaluate. Cannot be null or empty.</param>
        /// <returns>True if the word is a stop word and should be removed; otherwise false.</returns>
        /// <exception cref="ArgumentNullException">Thrown when word is null.</exception>
        /// <exception cref="ArgumentException">Thrown when word is empty or whitespace.</exception>
        bool IsStopWord(string word);
    }
}