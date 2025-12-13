namespace Verbex
{
    using System;

    /// <summary>
    /// Represents the position of a term occurrence within a document.
    /// </summary>
    /// <remarks>
    /// This class is used for in-memory representation during document processing.
    /// Positions are stored in the database as separate integer arrays for
    /// character positions and term positions.
    /// </remarks>
    public class TermPosition
    {
        /// <summary>
        /// The character position (absolute offset from the start of the document).
        /// </summary>
        public int CharacterPosition { get; set; }

        /// <summary>
        /// The term position (0-based index of the word in the document).
        /// </summary>
        public int TermIndex { get; set; }

        /// <summary>
        /// Creates a new instance of TermPosition.
        /// </summary>
        public TermPosition()
        {
        }

        /// <summary>
        /// Creates a new instance of TermPosition with specified positions.
        /// </summary>
        /// <param name="characterPosition">The character position (absolute offset).</param>
        /// <param name="termIndex">The term position (word index).</param>
        public TermPosition(int characterPosition, int termIndex)
        {
            CharacterPosition = characterPosition;
            TermIndex = termIndex;
        }

        /// <summary>
        /// Returns a string representation of the position.
        /// </summary>
        /// <returns>String in format "characterPosition:termIndex".</returns>
        public override string ToString()
        {
            return $"{CharacterPosition}:{TermIndex}";
        }

        /// <summary>
        /// Determines equality based on both position values.
        /// </summary>
        /// <param name="obj">Object to compare.</param>
        /// <returns>True if equal.</returns>
        public override bool Equals(object? obj)
        {
            if (obj is TermPosition other)
            {
                return CharacterPosition == other.CharacterPosition && TermIndex == other.TermIndex;
            }
            return false;
        }

        /// <summary>
        /// Returns hash code based on both position values.
        /// </summary>
        /// <returns>Hash code.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(CharacterPosition, TermIndex);
        }
    }
}
