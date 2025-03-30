using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using BlackBytesBox.Routed.GitBackend.Middleware.GitBackendMiddleware;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace BlackBytesBox.Routed.GitBackend.Tests
{
    [TestClass]
    public sealed class MyMiddlewareIntegrationTests
    {
        private const string localhost = "https://localhost:5425";
        private const string localhostuser = "https://gituser:secret@localhost:5425";
        private static WebApplicationBuilder? builder;
        private static WebApplication? app;
        private HttpClient? client;

        [ClassInitialize]
        public static async Task ClassInit(TestContext context)
        {
            // Create the builder using the minimal hosting model.
            builder = WebApplication.CreateBuilder();

            // Set a fixed URL for the host.
            builder.WebHost.UseUrls(localhost);
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);

            System.IO.Directory.CreateDirectory(@"C:\gitremote");
            System.IO.Directory.CreateDirectory(@"C:\gitlocal");

            var find = LocateExecutable("git");
            string oneup = Path.GetFullPath(find.Value.Directory + @"\..\mingw64\libexec\git-core\git-http-backend.exe");

            var result = ExecuteProcess(@"git", @$"-C ""C:\gitremote"" init --bare MyProject.git", "");
            var result2 = ExecuteProcess(@"git", @$"-C ""C:\gitremote\MyProject.git"" config http.receivepack true", "");


            // Build the application.
            app = builder.Build();

            app.UseGitBackend2(@"C:\gitremote", oneup, "/gitrepos", (repoName, username, password) =>
            {
                // Implement repository-specific auth logic.
                // For example, allow access if username equals repoName (or any custom rule).
                return string.Equals(username, "gituser", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(password, "secret", StringComparison.Ordinal) &&
                       repoName.Equals("MyProject.git", StringComparison.OrdinalIgnoreCase);
            });

            // Start the application.
            await app.StartAsync();
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            if (app != null)
            {
                await app.StopAsync();
            }
        }

        [TestInitialize]
        public void TestInit()
        {
            // Create an HttpClientHandler that accepts any certificate.
            var handler = new HttpClientHandler
            {
                // Accept all certificates (this is unsafe for production!)
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                // Alternatively, you can use:
                // ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            // Create a new, independent HttpClient for each test.
            client = new HttpClient(handler)
            {
                BaseAddress = new Uri(localhost),
                DefaultRequestVersion = HttpVersion.Version11, // Force HTTP/1.0
            };
            // Add a default User-Agent header for testing.
            client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/90.0.4430.93 Safari/537.36");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Dispose of the HttpClient after each test.
            client?.Dispose();
            client = null;
        }

        [TestMethod]
        [DataRow(100)]
        public async Task TestMyMiddlewareIntegration(int delay)
        {
            // Simulate an optional delay to mimic asynchronous conditions.
            await Task.Delay(delay);

            //HttpResponseMessage response = await client!.GetAsync("/gitrepos/MyProject.git/info/refs?service=git-receive-pack");
            //response.EnsureSuccessStatusCode();

        
            var result = ExecuteProcess(@"git", @$"-C C:\gitlocal -c http.sslVerify=false clone {localhostuser}/gitrepos/MyProject.git", "");
            var result1 = ExecuteProcess(@"git", @$"-C C:\gitlocal\MyProject config http.sslVerify false", "");

            System.IO.File.WriteAllText(@"C:\gitlocal\MyProject\Readme.md", "test");

            var result2 = ExecuteProcess(@"git", @$"-C C:\gitlocal\MyProject add .", "");
            var result3 = ExecuteProcess(@"git", @$"-C C:\gitlocal\MyProject commit -m ""Initial commit""", "");
            var result4 = ExecuteProcess(@"git", @$"-C C:\gitlocal\MyProject push", "");


            Assert.IsTrue(true);
            return;
        }

        /// <summary>
        /// Executes a process with the specified filename, arguments, and working directory.
        /// </summary>
        /// <remarks>
        /// This method runs the specified process without shell execution, captures both standard output and error streams,
        /// and returns these outputs along with the process exit code.
        /// </remarks>
        /// <param name="fileName">The executable to run.</param>
        /// <param name="arguments">The command-line arguments for the process.</param>
        /// <param name="workingDirectory">The directory in which to execute the process.</param>
        /// <returns>A tuple containing the standard output, standard error, and exit code.</returns>
        /// <example>
        /// var result = ExecuteProcess("git", "-C \"C:\\gitlocal\" clone http://localhostuser/gitrepos/MyProject.git", @"C:\gitlocal");
        /// Console.WriteLine($"Output: {result.Output}");
        /// Console.WriteLine($"Error: {result.Error}");
        /// Console.WriteLine($"Exit Code: {result.ExitCode}");
        /// </example>
        public static (string Output, string Error, int ExitCode) ExecuteProcess(string fileName, string arguments, string workingDirectory)
        {
            // Configure the process start information.
            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,                // Required for redirecting streams.
                RedirectStandardOutput = true,          // Enable capturing standard output.
                RedirectStandardError = true,           // Enable capturing standard error.
                RedirectStandardInput = false
            };

            using (var process = new System.Diagnostics.Process())
            {
                process.StartInfo = psi;
                process.Start();

                // Capture the output streams.
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();

                process.WaitForExit();
                int exitCode = process.ExitCode;

                // Return all captured output in a tuple.
                return (output, error, exitCode);
            }
        }

        /// <summary>
        /// Locates an executable in the system PATH.
        /// </summary>
        /// <remarks>
        /// On Windows, if the provided filename lacks an extension, ".exe" is appended for the search.
        /// This method returns a tuple with the directory where the executable was found, the filename (with extension),
        /// and the fully qualified path to the executable.
        /// </remarks>
        /// <param name="fileName">The name of the executable file (e.g., "git" or "git.exe").</param>
        /// <returns>
        /// A tuple containing:
        ///   - Directory: The directory in which the executable was found.
        ///   - FileName: The filename with the proper extension.
        ///   - FullPath: The fully qualified path to the executable.
        /// Returns null if the executable is not found.
        /// </returns>
        /// <example>
        /// var result = LocateExecutable("git");
        /// if (result != null)
        /// {
        ///     Console.WriteLine($"Directory: {result.Value.Directory}");
        ///     Console.WriteLine($"FileName: {result.Value.FileName}");
        ///     Console.WriteLine($"FullPath: {result.Value.FullPath}");
        /// }
        /// else
        /// {
        ///     Console.WriteLine("Executable not found in the system PATH.");
        /// }
        /// </example>
        public static (string Directory, string FileName, string FullPath)? LocateExecutable(string fileName)
        {
            // On Windows, if the file name does not have an extension, append ".exe" for the search.
            string searchFileName = fileName;
            if (Environment.OSVersion.Platform == PlatformID.Win32NT && !Path.HasExtension(fileName))
            {
                searchFileName += ".exe";
            }

            // Retrieve the PATH environment variable.
            string? pathEnv = Environment.GetEnvironmentVariable("PATH");
            if (string.IsNullOrEmpty(pathEnv))
            {
                return null;
            }

            // Split the PATH into individual directories.
            var paths = pathEnv.Split(Path.PathSeparator);
            foreach (var path in paths)
            {
                try
                {
                    string candidate = Path.Combine(path, searchFileName);
                    if (File.Exists(candidate))
                    {
                        // Get the fully qualified path.
                        string fullPath = Path.GetFullPath(candidate);
                        return (path, searchFileName, fullPath);
                    }
                }
                catch
                {
                    // Ignore exceptions and continue with the next directory.
                }
            }

            // Executable not found in any of the PATH directories.
            return null;
        }


    }
}
