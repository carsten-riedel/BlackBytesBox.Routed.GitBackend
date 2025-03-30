using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.AspNetCore.Http.Extensions;
using System.Net;
using BlackBytesBox.Routed.GitBackend.Utility;
using System.Runtime.Intrinsics.X86;
using BlackBytesBox.Routed.GitBackend.Extensions.EnumerableExtensions;

namespace BlackBytesBox.Routed.GitBackend.Middleware.GitBackendMiddleware
{
    /// <summary>
    /// Middleware that integrates Git’s smart HTTP backend by executing git-http-backend.exe.
    /// It sets up required CGI environment variables and streams request/response data to enable
    /// basic Git clone, fetch, and push operations. It supports Basic authentication by calling
    /// a delegate for repository-specific credential validation.
    /// </summary>
    public class GitBackendMiddleware2
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GitBackendMiddleware2> _logger;
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
        public GitBackendMiddleware2(RequestDelegate next, ILogger<GitBackendMiddleware2> logger, string repositoryRoot, string gitHttpBackendPath, string basePath, Func<string, string, string, bool> validateCredentials)
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

            var uri = HttpContextUtility.GetUriFromRequestDisplayUrl(context, _logger);
            if (uri is null)
            {
                await _next(context);
                return;
            }

            //No url encoding
            if (uri.Segments.Any(e => WebUtility.UrlDecode(e) != e))
            {
                await _next(context);
                return;
            }

            //No double slashes besides the start, preventing // in the middle of the url
            if (uri.Segments.Count(e => string.IsNullOrWhiteSpace(e.Trim('/'))) != 1)
            {
                await _next(context);
                return;
            }

            List<string> normalizedUriSegments = uri.Segments.Select(e => e.Trim('/')).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

            //No empty segments
            if (normalizedUriSegments.Count == 0)
            {
                await _next(context);
                return;
            }

            //No path traversal or .git
            if (normalizedUriSegments.Any(segment => segment.Equals("..") || segment.Equals(".") || segment.Equals(".git", StringComparison.OrdinalIgnoreCase)))
            {
                await _next(context);
                return;
            }

            // Check if any segment contains an illegal path character.
            if (normalizedUriSegments.Any(segment => segment.IndexOfAny(Path.GetInvalidPathChars()) != -1))
            {
                await _next(context);
                return;
            }

            // Check if any segment contains an illegal file name character.
            if (normalizedUriSegments.Any(segment => segment.IndexOfAny(Path.GetInvalidFileNameChars()) != -1))
            {
                await _next(context);
                return;
            }

            //more or missing .git endings
            if (normalizedUriSegments.Count(s => s.EndsWith(".git", StringComparison.OrdinalIgnoreCase)) != 1)
            {
                await _next(context);
                return;
            }

            var gitRepoPath = normalizedUriSegments.TakeWhile(e => !e.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList();
            var remainingRepoPath = string.Join("", normalizedUriSegments.SkipWhile(e => !e.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList().Select(e=> "/"+e).ToList());
            
            var gitRepoName = normalizedUriSegments.Where(s => s.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList().First();
            var repoDepth = gitRepoPath.Count;

            List<string> gitDepthRoot = new List<string>();
            gitDepthRoot.Add(_repositoryRoot);
            gitDepthRoot.Add(repoDepth.ToString());
            gitDepthRoot.AddRange(gitRepoPath);

            var gitDepthRootString = System.IO.Path.Combine(gitDepthRoot.ToArray());

            System.IO.Directory.CreateDirectory(gitDepthRootString);
 

            // --- Authentication Check ---
            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                await WriteUnauthorizedResponseAsync(context, "Unauthorized: Missing Authorization header.");
                return;
            }

            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
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
                    await WriteUnauthorizedResponseAsync(context, "Unauthorized: Invalid Base64 encoding.");
                    return;
                }
                var parts = decodedCredentials.Split(new char[] { ':' }, 2);
                if (parts.Length != 2)
                {
                    await WriteUnauthorizedResponseAsync(context,"Unauthorized: Invalid credentials format.");
                    return;
                }
                var username = parts[0];
                var password = parts[1];

                // Use the injected delegate to validate credentials for the given repository.
                if (!_validateCredentials(gitRepoName, username, password))
                {
                    await WriteUnauthorizedResponseAsync(context, "Unauthorized: Invalid username or password for this repository.");
                    return;
                }
            }
            else
            {
                await WriteUnauthorizedResponseAsync(context, "Unauthorized: Unsupported authorization method.",false);
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

            psi.Environment["GIT_PROJECT_ROOT"] = gitDepthRootString;
            psi.Environment["GIT_HTTP_EXPORT_ALL"] = "1";
            psi.Environment["REQUEST_METHOD"] = context.Request.Method;
            psi.Environment["QUERY_STRING"] = context.Request.QueryString.HasValue
                ? context.Request.QueryString.Value.TrimStart('?')
                : "";
            psi.Environment["CONTENT_TYPE"] = context.Request.ContentType ?? "";
            psi.Environment["CONTENT_LENGTH"] = context.Request.ContentLength?.ToString() ?? "0";
            psi.Environment["PATH_INFO"] = remainingRepoPath.ToString();
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

        private bool IsRequestPathValid(HttpContext context, out PathString remainingPath)
        {
            // Implementation hidden: returns true if the request path starts with _basePath.
            return context.Request.Path.StartsWithSegments(_basePath, out remainingPath);
        }

        /// <summary>Writes a 401 Unauthorized response.</summary>
        /// <param name="context">HTTP context.</param>
        /// <param name="responseMessage">Response message.</param>
        /// <param name="includeRealmHeader">Include WWW-Authenticate header (default true).</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        private async Task WriteUnauthorizedResponseAsync(HttpContext context, string responseMessage, bool includeRealmHeader = true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            if (includeRealmHeader)
            {
                context.Response.Headers.WWWAuthenticate = @"Basic realm=""Git Repository""";
            }
            await context.Response.WriteAsync(responseMessage);
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
