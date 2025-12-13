namespace Verbex
{
    using System;

    /// <summary>
    /// Interface for text lemmatization, reducing words to their base or dictionary form.
    /// </summary>
    public interface ILemmatizer
    {
        /// <summary>
        /// Reduces a word to its base form.
        /// </summary>
        /// <param name="word">The word to lemmatize. Cannot be null or empty.</param>
        /// <returns>The lemmatized form of the word.</returns>
        /// <exception cref="ArgumentNullException">Thrown when word is null.</exception>
        /// <exception cref="ArgumentException">Thrown when word is empty or whitespace.</exception>
        string Lemmatize(string word);
    }
}