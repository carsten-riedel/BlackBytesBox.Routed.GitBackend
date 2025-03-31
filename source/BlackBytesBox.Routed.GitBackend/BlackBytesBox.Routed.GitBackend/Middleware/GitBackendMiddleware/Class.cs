namespace BlackBytesBox.Routed.GitBackend.Middleware.GitBackendMiddleware
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents the repository configuration settings.
    /// </summary>
    public class RepositorySettings
    {
        /// <summary>
        /// Gets or sets the list of repository accounts.
        /// </summary>
        public List<RepositoryAccount> RepositoryAccounts { get; set; } = new List<RepositoryAccount>();

        /// <summary>
        /// Gets or sets the list of repository mappings.
        /// </summary>
        public List<RepositoryMapping> RepositoryMappings { get; set; } = new List<RepositoryMapping>();
    }

    /// <summary>
    /// Represents an account with basic authentication details.
    /// </summary>
    public class RepositoryAccount
    {
        /// <summary>
        /// Gets or sets the account name.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// Gets or sets the hashed password.
        /// </summary>
        public string PasswordHash { get; set; }

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
        public string Username { get; set; }

        /// <summary>
        /// Gets or sets the hashed password.
        /// </summary>
        public string PasswordHash { get; set; }
    }

    /// <summary>
    /// Represents a mapping between a repository path and allowed account names.
    /// </summary>
    public class RepositoryMapping
    {
        /// <summary>
        /// Gets or sets the repository path.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Gets or sets the list of account names allowed to access the repository.
        /// </summary>
        public List<string> AccountNames { get; set; } = new List<string>();
    }

}
