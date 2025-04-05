using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace BlackBytesBox.Routed.GitBackend.Middleware.GitBackendMiddleware
{
    /// <summary>
    /// Middleware that integrates Git’s smart HTTP backend by executing git-http-backend.exe.
    /// It sets up required CGI environment variables and streams request/response data to enable
    /// basic Git clone, fetch, and push operations. It supports Basic authentication by calling
    /// a delegate for repository-specific credential validation.
    /// </summary>
    public class GitBackendMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GitBackendMiddleware> _logger;
        private readonly string _repositoryRoot;
        private readonly string _gitHttpBackendPath;
        private readonly string _basePath;
        private readonly Func<string, string, string, bool> _validateCredentials;

        /// <summary>
        /// Initializes a new instance of the <see cref="GitBackendMiddleware"/> class.
        /// </summary>
        /// <param name="next">The next middleware in the pipeline.</param>
        /// <param name="repositoryRoot">The root directory containing bare Git repositories.</param>
        /// <param name="gitHttpBackendPath">The full path to git-http-backend.exe.</param>
        /// <param name="basePath">The URL base path that should be handled by this middleware (e.g. "/gitrepos").</param>
        /// <param name="validateCredentials">
        /// A delegate that receives the repository name, username, and password, and returns true if the credentials are valid for that repo.
        /// </param>
        public GitBackendMiddleware(RequestDelegate next, ILogger<GitBackendMiddleware> logger, string repositoryRoot, string gitHttpBackendPath, string basePath, Func<string, string, string, bool> validateCredentials)
        {
            _next = next;
            _logger = logger;
            _repositoryRoot = repositoryRoot;
            _gitHttpBackendPath = gitHttpBackendPath;
            _basePath = basePath?.TrimEnd('/') ?? "";
            _validateCredentials = validateCredentials;
        }

        /// <summary>
        /// Processes Git HTTP requests by launching git-http-backend.exe with the proper CGI environment,
        /// and validates the request using Basic authentication via the provided delegate.
        /// </summary>
        /// <param name="context">The current HTTP context.</param>
        /// <returns>A task representing asynchronous execution.</returns>
        public async Task InvokeAsync(HttpContext context)
        {
            // Only process requests that start with the specified base path.
            if (!context.Request.Path.StartsWithSegments(_basePath, out var remainingPath))
            {
                await _next(context);
                return;
            }

            // Expect the URL to contain a repository reference (i.e. ".git").
            if (!remainingPath.Value.Contains(".git", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Extract the repository name.
            if (!TryExtractRepositoryName(remainingPath, out var repoName))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden: Invalid repository path structure.");
                return;
            }

            // Build and validate the repository path.
            var repoPathCandidate = Path.Combine(_repositoryRoot, repoName);
            var normalizedRepoPath = Path.GetFullPath(repoPathCandidate);
            var normalizedRepoRoot = Path.GetFullPath(_repositoryRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (!normalizedRepoPath.StartsWith(normalizedRepoRoot, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsync("Forbidden: Invalid repository path.");
                return;
            }
            if (!Directory.Exists(normalizedRepoPath) || !File.Exists(Path.Combine(normalizedRepoPath, "HEAD")))
            {
                await _next(context);
                return;
            }

            // --- Authentication Check ---
            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Git Repository\"";
                await context.Response.WriteAsync("Unauthorized: Missing Authorization header.");
                return;
            }

            var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
            if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var encodedCredentials = authHeader.Substring("Basic ".Length).Trim();
                string decodedCredentials;
                try
                {
                    var credentialBytes = Convert.FromBase64String(encodedCredentials);
                    decodedCredentials = Encoding.UTF8.GetString(credentialBytes);
                }
                catch
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Git Repository\"";
                    await context.Response.WriteAsync("Unauthorized: Invalid Base64 encoding.");
                    return;
                }
                var parts = decodedCredentials.Split(new char[] { ':' }, 2);
                if (parts.Length != 2)
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Git Repository\"";
                    await context.Response.WriteAsync("Unauthorized: Invalid credentials format.");
                    return;
                }
                var username = parts[0];
                var password = parts[1];

                // Use the injected delegate to validate credentials for the given repository.
                if (!_validateCredentials(repoName, username, password))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Git Repository\"";
                    await context.Response.WriteAsync("Unauthorized: Invalid username or password for this repository.");
                    return;
                }
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsync("Unauthorized: Unsupported authorization method.");
                return;
            }
            // --- End Authentication Check ---

            // Configure and execute git-http-backend.exe.
            var psi = new ProcessStartInfo
            {
                FileName = _gitHttpBackendPath,
                WorkingDirectory = Path.GetDirectoryName(_gitHttpBackendPath),
                Arguments = string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
            };
            psi.Environment["GIT_PROJECT_ROOT"] = _repositoryRoot;
            psi.Environment["GIT_HTTP_EXPORT_ALL"] = "1";
            psi.Environment["REQUEST_METHOD"] = context.Request.Method;
            psi.Environment["QUERY_STRING"] = context.Request.QueryString.HasValue
                ? context.Request.QueryString.Value.TrimStart('?')
                : "";
            psi.Environment["CONTENT_TYPE"] = context.Request.ContentType ?? "";
            psi.Environment["CONTENT_LENGTH"] = context.Request.ContentLength?.ToString() ?? "0";
            psi.Environment["PATH_INFO"] = remainingPath.ToString();
            psi.Environment["REMOTE_ADDR"] = context.Connection.RemoteIpAddress?.ToString() ?? "";
            psi.Environment["SERVER_PROTOCOL"] = context.Request.Protocol;
            psi.Environment["SERVER_SOFTWARE"] = "Kestrel";
            if (context.Request.Path.Value.Contains("git-receive-pack", StringComparison.OrdinalIgnoreCase))
            {
                psi.Environment["REMOTE_USER"] = "anonymous";
            }

            using (var process = new Process { StartInfo = psi })
            {
                process.Start();
                if (context.Request.ContentLength > 0)
                {
                    await context.Request.Body.CopyToAsync(process.StandardInput.BaseStream);
                    process.StandardInput.Close();
                }
                else
                {
                    process.StandardInput.Close();
                }

                var errorTask = process.StandardError.ReadToEndAsync();
                var reader = process.StandardOutput;
                string? line;
                var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                int statusCode = 200;

                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        break;
                    int separatorIndex = line.IndexOf(':');
                    if (separatorIndex > 0)
                    {
                        var headerName = line.Substring(0, separatorIndex).Trim();
                        var headerValue = line.Substring(separatorIndex + 1).Trim();
                        if (headerName.Equals("Status", StringComparison.OrdinalIgnoreCase))
                        {
                            if (int.TryParse(headerValue.Split(' ')[0], out int code))
                                statusCode = code;
                        }
                        else
                        {
                            headers[headerName] = headerValue;
                        }
                    }
                }
                context.Response.StatusCode = statusCode;
                foreach (var header in headers)
                {
                    context.Response.Headers[header.Key] = header.Value;
                }
                await reader.BaseStream.CopyToAsync(context.Response.Body);
                process.WaitForExit();
                string stdError = await errorTask;
                if (!string.IsNullOrWhiteSpace(stdError))
                {
                    _logger.LogError("git-http-backend error: " + stdError);
                }
            }
        }

        /// <summary>
        /// Extracts and normalizes the repository name from the remaining URL path.
        /// This method forces normalization (resolving any ".." segments) and ensures that
        /// the first segment ends with ".git".
        /// </summary>
        /// <param name="remainingPath">The URL path remaining after the base path.</param>
        /// <param name="repoName">The extracted repository name, if valid.</param>
        /// <returns>True if extraction is successful; otherwise, false.</returns>
        private bool TryExtractRepositoryName(PathString remainingPath, out string repoName)
        {
            repoName = string.Empty;
            // Remove leading '/' and combine with a dummy base to force normalization.
            string rawRelativePath = remainingPath.Value.TrimStart('/');
            const string dummyBase = "dummy";
            string combinedPath = Path.Combine(dummyBase, rawRelativePath);
            string normalizedFullPath = Path.GetFullPath(combinedPath);
            string dummyFullPath = Path.GetFullPath(dummyBase) + Path.DirectorySeparatorChar;
            string normalizedRelativePath = normalizedFullPath.StartsWith(dummyFullPath, StringComparison.OrdinalIgnoreCase)
                ? normalizedFullPath.Substring(dummyFullPath.Length)
                : normalizedFullPath;

            // Split the normalized path.
            var segments = normalizedRelativePath.Split(new[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                return false;
            }
            // The first segment should be the repository name and must end with ".git".
            if (!segments[0].EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            repoName = segments[0];
            return true;
        }
    }

}
