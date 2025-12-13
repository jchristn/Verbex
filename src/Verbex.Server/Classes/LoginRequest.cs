namespace Verbex.Server.Classes
{
    using System;

    /// <summary>
    /// Login request.
    /// </summary>
    public class LoginRequest
    {
        #region Public-Members

        /// <summary>
        /// Username.
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
        /// Password.
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
        /// Instantiate.
        /// </summary>
        public LoginRequest()
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
            if (String.IsNullOrEmpty(_Username))
            {
                errorMessage = "Username is required";
                return false;
            }

            if (String.IsNullOrEmpty(_Password))
            {
                errorMessage = "Password is required";
                return false;
            }

            errorMessage = "";
            return true;
        }

        #endregion
    }
}
