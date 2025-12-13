namespace Verbex.Server.Classes
{
    /// <summary>
    /// Debug settings.
    /// </summary>
    public class DebugSettings
    {
        #region Public-Members

        /// <summary>
        /// Enable authentication debugging.
        /// </summary>
        public bool Authentication { get; set; } = false;

        /// <summary>
        /// Enable exception debugging.
        /// </summary>
        public bool Exceptions { get; set; } = true;

        /// <summary>
        /// Enable request debugging.
        /// </summary>
        public bool Requests { get; set; } = false;

        /// <summary>
        /// Enable database query debugging.
        /// </summary>
        public bool DatabaseQueries { get; set; } = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public DebugSettings()
        {

        }

        #endregion
    }
}