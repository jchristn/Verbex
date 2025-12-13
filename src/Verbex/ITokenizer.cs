namespace Verbex
{
    using System;

    /// <summary>
    /// Interface for tokenizing text content into individual terms
    /// </summary>
    public interface ITokenizer
    {
        /// <summary>
        /// Tokenizes the given content into an array of tokens
        /// </summary>
        /// <param name="content">The content to tokenize</param>
        /// <returns>Array of tokens extracted from the content</returns>
        /// <exception cref="ArgumentNullException">Thrown when content is null</exception>
        string[] Tokenize(string content);
    }
}