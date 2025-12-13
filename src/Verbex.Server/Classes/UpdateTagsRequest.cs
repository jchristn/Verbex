namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Request to update tags on an index or document.
    /// </summary>
    public class UpdateTagsRequest
    {
        #region Public-Members

        /// <summary>
        /// Tags to set (full replacement).
        /// </summary>
        public Dictionary<string, string> Tags
        {
            get
            {
                return _Tags;
            }
            set
            {
                _Tags = value ?? new Dictionary<string, string>();
            }
        }

        #endregion

        #region Private-Members

        private Dictionary<string, string> _Tags = new Dictionary<string, string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public UpdateTagsRequest()
        {

        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Validate the request.
        /// </summary>
        /// <param name="errorMessage">Error message if validation fails.</param>
        /// <returns>True if valid, false otherwise.</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = "";
            return true;
        }

        #endregion
    }
}
