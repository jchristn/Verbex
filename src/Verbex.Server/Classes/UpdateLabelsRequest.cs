namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Request to update labels on an index or document.
    /// </summary>
    public class UpdateLabelsRequest
    {
        #region Public-Members

        /// <summary>
        /// Labels to set (full replacement).
        /// </summary>
        public List<string> Labels
        {
            get
            {
                return _Labels;
            }
            set
            {
                _Labels = value ?? new List<string>();
            }
        }

        #endregion

        #region Private-Members

        private List<string> _Labels = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public UpdateLabelsRequest()
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
