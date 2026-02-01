namespace Verbex.DTO
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents position data for a term within a document.
    /// </summary>
    public class TermPositionData
    {
        #region Public-Members

        /// <summary>
        /// Character positions where the term appears in the document content.
        /// </summary>
        public List<int> CharacterPositions
        {
            get => _CharacterPositions;
            set => _CharacterPositions = value ?? new List<int>();
        }

        /// <summary>
        /// Term positions (word indices) where the term appears in the document.
        /// </summary>
        public List<int> TermPositions
        {
            get => _TermPositions;
            set => _TermPositions = value ?? new List<int>();
        }

        #endregion

        #region Private-Members

        private List<int> _CharacterPositions = new List<int>();
        private List<int> _TermPositions = new List<int>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TermPositionData()
        {
        }

        /// <summary>
        /// Instantiate with values.
        /// </summary>
        /// <param name="characterPositions">Character positions.</param>
        /// <param name="termPositions">Term positions.</param>
        public TermPositionData(List<int> characterPositions, List<int> termPositions)
        {
            _CharacterPositions = characterPositions ?? new List<int>();
            _TermPositions = termPositions ?? new List<int>();
        }

        #endregion
    }
}
