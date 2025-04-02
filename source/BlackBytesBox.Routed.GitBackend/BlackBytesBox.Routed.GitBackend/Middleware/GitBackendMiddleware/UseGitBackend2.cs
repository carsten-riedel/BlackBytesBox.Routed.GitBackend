using Microsoft.AspNetCore.Builder;
using System;

namespace BlackBytesBox.Routed.GitBackend.Middleware.GitBackendMiddleware
{
    /// <summary>
    /// Extension methods for adding the Git backend middleware to the ASP.NET Core pipeline.
    /// </summary>
    public static partial class GitBackendMiddlewareExtensions
    {
        /// <summary>
        /// Adds the Git backend middleware to the pipeline with a custom base path and repository-specific credential validation.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="repositoryRoot">The folder that contains bare Git repositories.</param>
        /// <param name="gitHttpBackendPath">The full path to git-http-backend.exe.</param>
        /// <param name="basePath">The URL prefix to match (e.g. "/gitrepos").</param>
        /// <param name="validateCredentials">
        /// A delegate to validate the repository name, username, and password.
        /// </param>
        /// <returns>The updated application builder.</returns>
        public static IApplicationBuilder UseGitBackend2(this IApplicationBuilder builder, string gitHttpBackendPath)
        {
            return builder.UseMiddleware<GitBackendMiddleware2>(gitHttpBackendPath);
        }
    }
}
