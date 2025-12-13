namespace Verbex.Sdk
{
    using System;

    /// <summary>
    /// Request body for user authentication.
    /// </summary>
    public class LoginRequest
    {
        #region Public-Members

        /// <summary>
        /// Username for authentication.
        /// </summary>
        public string Username
        {
            get
            {
                return _Username;
            }
            set
            {
                _Username = value ?? "";
            }
        }

        /// <summary>
        /// Password for authentication.
        /// </summary>
        public string Password
        {
            get
            {
                return _Password;
            }
            set
            {
                _Password = value ?? "";
            }
        }

        #endregion

        #region Private-Members

        private string _Username = "";
        private string _Password = "";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate an empty LoginRequest.
        /// </summary>
        public LoginRequest()
        {
        }

        /// <summary>
        /// Instantiate a LoginRequest with credentials.
        /// </summary>
        /// <param name="username">Username for authentication.</param>
        /// <param name="password">Password for authentication.</param>
        public LoginRequest(string username, string password)
        {
            Username = username;
            Password = password;
        }

        #endregion
    }
}
