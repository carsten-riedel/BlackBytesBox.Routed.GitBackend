using System.Collections.Generic;

namespace BlackBytesBox.Routed.GitBackend.Middleware.GitBackendMiddleware
{
    /// <summary>
    /// Represents the repository configuration settings.
    /// </summary>
    public class BackendSettings
    {
        public string GitCommandRoot { get; set; } = string.Empty;

        public string GitCommandFilePath { get; set; } = string.Empty;

        public string GitBackendFilePath { get; set; } = string.Empty;

        public string GitRepositorysDirectory { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of repository accounts.
        /// </summary>
        public List<Account> Accounts { get; set; } = new List<Account>();

        /// <summary>
        /// Gets or sets the list of repository mappings.
        /// </summary>
        public List<AccessRight> AccessRights { get; set; } = new List<AccessRight>();
    }

    /// <summary>
    /// Represents an account with basic authentication details.
    /// </summary>
    public class Account
    {
        /// <summary>
        /// Gets or sets the account name.
        /// </summary>
        public string AccountName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hashed password.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        public string PasswordType { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of basic authentication credentials.
        /// </summary>
        public List<BasicAuth> BasicAuths { get; set; } = new List<BasicAuth>();
    }

    /// <summary>
    /// Represents a basic authentication credential.
    /// </summary>
    public class BasicAuth
    {
        /// <summary>
        /// Gets or sets the username.
        /// </summary>
        public string Username { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the hashed password.
        /// </summary>
        public string Password { get; set; } = string.Empty;

        public string PasswordType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a mapping between a repository path and allowed account names.
    /// </summary>
    public class AccessRight
    {
        /// <summary>
        /// Gets or sets the repository path.
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the list of account names allowed to access the repository.
        /// </summary>
        public List<string> AccountNames { get; set; } = new List<string>();
    }

}
