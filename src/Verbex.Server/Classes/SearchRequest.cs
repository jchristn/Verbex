namespace Verbex.Server.Classes
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Search request with optional label and tag filters.
    /// </summary>
    public class SearchRequest
    {
        #region Public-Members

        /// <summary>
        /// Search query string.
        /// </summary>
        public string Query
        {
            get
            {
                return _Query;
            }
            set
            {
                _Query = value ?? "";
            }
        }

        /// <summary>
        /// Maximum number of results to return.
        /// Default value is 100.
        /// </summary>
        public int MaxResults
        {
            get
            {
                return _MaxResults;
            }
            set
            {
                _MaxResults = value < 1 ? 100 : value;
            }
        }

        /// <summary>
        /// If true, documents must contain ALL search terms (AND logic).
        /// If false, documents can contain ANY search term (OR logic).
        /// Default value is false (OR logic).
        /// </summary>
        public bool UseAndLogic
        {
            get
            {
                return _UseAndLogic;
            }
            set
            {
                _UseAndLogic = value;
            }
        }

        /// <summary>
        /// Optional list of labels to filter by.
        /// Documents must have ALL specified labels to match (AND logic).
        /// Label matching is case-insensitive.
        /// If null or empty, no label filtering is applied.
        /// </summary>
        public List<string>? Labels
        {
            get
            {
                return _Labels;
            }
            set
            {
                _Labels = value;
            }
        }

        /// <summary>
        /// Optional dictionary of tags (key-value pairs) to filter by.
        /// Documents must have ALL specified tags with matching values to match (AND logic).
        /// Tag matching is exact (case-sensitive for both key and value).
        /// If null or empty, no tag filtering is applied.
        /// </summary>
        public Dictionary<string, object>? Tags
        {
            get
            {
                return _Tags;
            }
            set
            {
                _Tags = value;
            }
        }

        #endregion

        #region Private-Members

        private string _Query = "";
        private int _MaxResults = 100;
        private bool _UseAndLogic = false;
        private List<string>? _Labels = null;
        private Dictionary<string, object>? _Tags = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        public SearchRequest()
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
            if (String.IsNullOrEmpty(_Query))
            {
                errorMessage = "Query is required";
                return false;
            }

            errorMessage = "";
            return true;
        }

        #endregion
    }
}
