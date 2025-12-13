namespace Verbex
{
    using System;

    /// <summary>
    /// Default implementation of ITokenizer that provides basic word tokenization
    /// Splits text on whitespace and common punctuation marks
    /// </summary>
    public class DefaultTokenizer : ITokenizer
    {
        #region Public-Members

        /// <summary>
        /// Characters used to split text into tokens
        /// </summary>
        public static readonly char[] DefaultSeparators = { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '[', ']', '{', '}', '"', '\'' };

        #endregion

        #region Private-Members

        private readonly char[] _Separators;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Initializes a new instance of the DefaultTokenizer class with default separators
        /// </summary>
        public DefaultTokenizer()
        {
            _Separators = DefaultSeparators;
        }

        /// <summary>
        /// Initializes a new instance of the DefaultTokenizer class with custom separators
        /// </summary>
        /// <param name="separators">Custom separator characters to use for tokenization</param>
        /// <exception cref="ArgumentNullException">Thrown when separators is null</exception>
        public DefaultTokenizer(char[] separators)
        {
            ArgumentNullException.ThrowIfNull(separators);
            _Separators = separators;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tokenizes the given content into an array of tokens
        /// Converts content to lowercase and splits on configured separator characters
        /// </summary>
        /// <param name="content">The content to tokenize</param>
        /// <returns>Array of lowercase tokens extracted from the content</returns>
        /// <exception cref="ArgumentNullException">Thrown when content is null</exception>
        public string[] Tokenize(string content)
        {
            ArgumentNullException.ThrowIfNull(content);

            return content.ToLowerInvariant()
                         .Split(_Separators, StringSplitOptions.RemoveEmptyEntries);
        }

        #endregion
    }
}