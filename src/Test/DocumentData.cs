namespace Test
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Helper class to hold document data for testing purposes
    /// </summary>
    public class DocumentData
    {
        private string _Id;
        private string _Path;
        private string _Content;
        private Dictionary<string, object>? _Metadata;

        /// <summary>
        /// Initializes a new instance of the DocumentData class
        /// </summary>
        public DocumentData()
        {
            _Id = string.Empty;
            _Path = string.Empty;
            _Content = string.Empty;
            _Metadata = null;
        }

        /// <summary>
        /// Gets or sets the unique document identifier
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when value is empty</exception>
        public string Id
        {
            get { return _Id; }
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("Document ID cannot be empty", nameof(value));
                }
                _Id = value;
            }
        }

        /// <summary>
        /// Gets or sets the document path or location
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
        public string Path
        {
            get { return _Path; }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _Path = value;
            }
        }

        /// <summary>
        /// Gets or sets the document content
        /// </summary>
        /// <exception cref="ArgumentNullException">Thrown when value is null</exception>
        public string Content
        {
            get { return _Content; }
            set
            {
                ArgumentNullException.ThrowIfNull(value);
                _Content = value;
            }
        }

        /// <summary>
        /// Gets or sets the optional metadata dictionary
        /// Can be null if no metadata is associated with the document
        /// </summary>
        public Dictionary<string, object>? Metadata
        {
            get { return _Metadata; }
            set { _Metadata = value; }
        }
    }
}