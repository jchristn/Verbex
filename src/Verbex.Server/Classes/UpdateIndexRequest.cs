namespace Verbex.Server.Classes
{
    using System;
    using Verbex;

    /// <summary>
    /// Request to update an index's core properties.
    /// </summary>
    public class UpdateIndexRequest
    {
        #region Public-Members

        /// <summary>
        /// New name for the index (optional, only update if provided).
        /// </summary>
        public string? Name
        {
            get
            {
                return _Name;
            }
            set
            {
                _Name = value;
            }
        }

        /// <summary>
        /// New description for the index (optional, only update if provided).
        /// </summary>
        public string? Description
        {
            get
            {
                return _Description;
            }
            set
            {
                _Description = value;
            }
        }

        /// <summary>
        /// New enabled status for the index (optional, only update if provided).
        /// </summary>
        public bool? Enabled
        {
            get
            {
                return _Enabled;
            }
            set
            {
                _Enabled = value;
            }
        }

        /// <summary>
        /// New cache configuration for the index (optional, only update if provided).
        /// Set to a new CacheConfiguration to enable/configure caching.
        /// </summary>
        public CacheConfiguration? CacheConfiguration
        {
            get
            {
                return _CacheConfiguration;
            }
            set
            {
                _CacheConfiguration = value;
            }
        }

        #endregion

        #region Private-Members

        private string? _Name = null;
        private string? _Description = null;
        private bool? _Enabled = null;
        private CacheConfiguration? _CacheConfiguration = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public UpdateIndexRequest()
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

            if (_Name != null && String.IsNullOrWhiteSpace(_Name))
            {
                errorMessage = "Name cannot be empty when provided";
                return false;
            }

            return true;
        }

        #endregion
    }
}
