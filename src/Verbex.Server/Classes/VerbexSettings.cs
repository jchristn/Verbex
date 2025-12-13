namespace Verbex.Server.Classes
{
    using System;

    /// <summary>
    /// Settings for the Verbex server.
    /// </summary>
    public class VerbexSettings
    {
        #region Public-Members

        /// <summary>
        /// Admin bearer token used for administrative operations.
        /// </summary>
        public string AdminBearerToken
        {
            get
            {
                return _AdminBearerToken;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(AdminBearerToken));
                _AdminBearerToken = value;
            }
        }

        /// <summary>
        /// Verbex repository filename.
        /// </summary>
        public string VerbexRepositoryFilename
        {
            get
            {
                return _VerbexRepositoryFilename;
            }
            set
            {
                if (String.IsNullOrEmpty(value)) throw new ArgumentNullException(nameof(VerbexRepositoryFilename));
                _VerbexRepositoryFilename = value;
            }
        }

        /// <summary>
        /// Maximum number of concurrent operations.
        /// </summary>
        public int MaxConcurrentOperations
        {
            get
            {
                return _MaxConcurrentOperations;
            }
            set
            {
                if (value < 1) throw new ArgumentOutOfRangeException(nameof(MaxConcurrentOperations));
                _MaxConcurrentOperations = value;
            }
        }

        /// <summary>
        /// Boolean indicating whether or not the repository should be in-memory.
        /// </summary>
        public bool InMemory
        {
            get
            {
                return _InMemory;
            }
            set
            {
                _InMemory = value;
            }
        }

        #endregion

        #region Private-Members

        private string _AdminBearerToken = "verbexadmin";
        private string _VerbexRepositoryFilename = "verbex.db";
        private int _MaxConcurrentOperations = 4;
        private bool _InMemory = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public VerbexSettings()
        {

        }

        #endregion
    }
}