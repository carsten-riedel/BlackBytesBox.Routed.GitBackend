using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using BlackBytesBox.Routed.GitBackend.Utility;
using BlackBytesBox.Routed.GitBackend.Utility.ProcessUtility;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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
        private readonly DynamicSettingsService<BackendSettings> _dynamicBackendSettingsService;

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
        public GitBackendMiddleware2(RequestDelegate next, ILogger<GitBackendMiddleware2> logger, DynamicSettingsService<BackendSettings> dynamicBackendSettingsService)
        {
            _next = next;
            _logger = logger;
            _dynamicBackendSettingsService = dynamicBackendSettingsService;

            _dynamicBackendSettingsService.OnChange += (settings) =>
            {
                _logger.LogInformation("Settings changed.");
                var repoList = settings.AccessRights.Select(e => e.Path).ToList();
                foreach (var repo in repoList)
                {
                    var segements = repo.Split('/');
                    List<string> gitRepoPathSegements = segements.TakeWhile(e => !e.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList();
                    string gitRepoName = segements.Where(s => s.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList().First();
                    var repoDepth = gitRepoPathSegements.Count;

                    string gitDepthRepoPath = System.IO.Path.Combine(new[] { settings.GitRepositorysDirectory, repoDepth.ToString() }.Concat(gitRepoPathSegements).ToArray());

                    if (!System.IO.Directory.Exists(Path.Combine(gitDepthRepoPath, gitRepoName)))
                    {
                        System.IO.Directory.CreateDirectory(gitDepthRepoPath);
                        var result = ProcessUtility.ExecuteProcess(@$"git", @$"-C ""{gitDepthRepoPath}"" init --bare {gitRepoName}", "");
                        var result2 = ProcessUtility.ExecuteProcess(@"git", @$"-C ""{Path.Combine(gitDepthRepoPath, gitRepoName)}"" config http.receivepack true", "");
                    }
                }

            };

            _dynamicBackendSettingsService.OnChangeWithSaveFunc += (settings) =>
            {
                //upgrade insecure passwords
                foreach (var account in settings.Accounts)
                {
                    if (account.PasswordType == "clear")
                    {
                        account.Password = string.Concat(System.Security.Cryptography.SHA1.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(account.Password)).Select(b => b.ToString("x2")));
                        account.PasswordType = "SHA1";
                    }
                }
                foreach (var account in settings.Accounts)
                {
                    foreach (var basic in account.BasicAuths)
                    {
                        if (basic.PasswordType == "clear")
                        {
                            basic.Password = string.Concat(System.Security.Cryptography.SHA1.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(basic.Password)).Select(b => b.ToString("x2")));
                            basic.PasswordType = "SHA1";
                        }
                    }
                }
                return settings;
            };
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

            //No url encoding allowed
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

            var settings = _dynamicBackendSettingsService.CurrentSettings;

            List<string> gitRepoPathSegements = normalizedUriSegments.TakeWhile(e => !e.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList();
            string gitRepoName = normalizedUriSegments.Where(s => s.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList().First();
            string gitRepoRemainingPath = string.Join("", normalizedUriSegments.SkipWhile(e => !e.EndsWith(".git", StringComparison.OrdinalIgnoreCase)).ToList().Select(e => "/" + e).ToList());
            int repoDepth = gitRepoPathSegements.Count;
            string gitDepthRepoPath = System.IO.Path.Combine(new[] { settings.GitRepositorysDirectory, repoDepth.ToString() }.Concat(gitRepoPathSegements).ToArray());
            string gitRepoPath = string.Join("/", gitRepoPathSegements) + @$"/{gitRepoName}";

            var basicAuthCheckResult = BasicAuthCheck(context);
            if (basicAuthCheckResult.IsInvalid)
            {
                await WriteUnauthorizedResponseAsync(context, basicAuthCheckResult.IsInvalidText, basicAuthCheckResult.IncludeRealmHeader);
                return;
            }

            var allowedAccounts = settings.AccessRights.Where(e => e.Path.Equals(gitRepoPath, StringComparison.OrdinalIgnoreCase)).SelectMany(e=>e.AccountNames).ToList();
            var allowedBasicAuthAccounts = settings.Accounts.Where(e => allowedAccounts.Contains(e.AccountName)).SelectMany(e => e.BasicAuths).ToList();

            bool allowed = false;
            foreach (var item in allowedBasicAuthAccounts)
            {
                if (item.Username.Equals(basicAuthCheckResult.Username))
                {
                    var settingsPw = item.Password;
                    var authPw = basicAuthCheckResult.Password;

                    if (item.PasswordType == "SHA1")
                    {
                        settingsPw = string.Concat(System.Security.Cryptography.SHA1.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(settingsPw)).Select(b => b.ToString("x2")));
                        authPw = string.Concat(System.Security.Cryptography.SHA1.Create().ComputeHash(System.Text.Encoding.UTF8.GetBytes(authPw)).Select(b => b.ToString("x2")));
                    }

                    if (settingsPw.Equals(authPw))
                    {
                        allowed = true;
                    }
                }
            }

            if (!allowed)
            {
                await WriteUnauthorizedResponseAsync(context, "Unauthorized: Invalid credentials.", false);
                return;
            }

            System.IO.Directory.CreateDirectory(gitDepthRepoPath);

            // Configure and execute git-http-backend.exe.
            var psi = new ProcessStartInfo
            {
                FileName = settings.GitBackendFilePath,
                WorkingDirectory = Path.GetDirectoryName(settings.GitBackendFilePath),
                Arguments = string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                RedirectStandardError = true,
            };

            psi.Environment["GIT_PROJECT_ROOT"] = gitDepthRepoPath;
            psi.Environment["GIT_HTTP_EXPORT_ALL"] = "1";
            psi.Environment["REQUEST_METHOD"] = context.Request.Method;
            psi.Environment["QUERY_STRING"] = context.Request.QueryString.HasValue
                ? context.Request.QueryString.Value.TrimStart('?')
                : "";
            psi.Environment["CONTENT_TYPE"] = context.Request.ContentType ?? "";
            psi.Environment["CONTENT_LENGTH"] = context.Request.ContentLength?.ToString() ?? "0";
            psi.Environment["PATH_INFO"] = gitRepoRemainingPath.ToString();
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

        public class BasicAuthResult
        {
            public string Username { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public bool IsInvalid { get; set; } = true;
            public string IsInvalidText { get; set; } = string.Empty;
            public bool IncludeRealmHeader { get; set; } = true;
        }

        public BasicAuthResult BasicAuthCheck(HttpContext context)
        {
            if (!context.Request.Headers.ContainsKey("Authorization"))
            {
                return new BasicAuthResult { IsInvalidText = "Unauthorized: Missing Authorization header." };
            }

            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();

            if (authHeader is null)
            {
                return new BasicAuthResult { IsInvalidText = "Unauthorized: Unsupported authorization method.", IncludeRealmHeader = false };
            }

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
                    return new BasicAuthResult { IsInvalidText = "Unauthorized: Invalid Base64 encoding." };
                }
                var parts = decodedCredentials.Split(new char[] { ':' }, 2);
                if (parts.Length != 2)
                {
                    return new BasicAuthResult { IsInvalidText = "Unauthorized: Invalid credentials format." };
                }
                var username = parts[0];
                var password = parts[1];

                return new BasicAuthResult { Username = parts[0], Password = parts[1], IsInvalidText = string.Empty, IsInvalid = false, IncludeRealmHeader = false };
            }
            else
            {
                return new BasicAuthResult { IsInvalidText = "Unauthorized: Unsupported authorization method.", IncludeRealmHeader = false };
            }
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
    }
}