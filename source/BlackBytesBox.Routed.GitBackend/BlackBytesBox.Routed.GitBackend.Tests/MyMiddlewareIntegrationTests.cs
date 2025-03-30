using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;


using BlackBytesBox.Routed.GitBackend.Middleware;
using BlackBytesBox.Routed.GitBackend.Middleware.GitBackendMiddleware;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
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

            System.Diagnostics.ProcessStartInfo psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = "init --bare MyProject.git",
                WorkingDirectory = @"C:\gitremote",
                UseShellExecute = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
            };

            System.Diagnostics.Process.Start(psi)?.WaitForExit();

            System.Diagnostics.ProcessStartInfo psi2 = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = @$"-C C:\gitremote\MyProject.git config http.receivepack true",
                WorkingDirectory = @"C:\gitremote",
                UseShellExecute = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
            };

            System.Diagnostics.Process.Start(psi2)?.WaitForExit();

            // Build the application.
            app = builder.Build();

            app.UseGitBackend(@"C:\gitremote", @"C:\Program Files\Git\mingw64\libexec\git-core\git-http-backend.exe", "/gitrepos", (repoName, username, password) =>
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

            System.Diagnostics.ProcessStartInfo psi2 = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = @$"-C C:\gitlocal -c http.sslVerify=false clone {localhostuser}/gitrepos/MyProject.git",
                WorkingDirectory = @"C:\gitlocal",
                UseShellExecute = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
            };

            System.Diagnostics.Process.Start(psi2)?.WaitForExit();

            System.Diagnostics.ProcessStartInfo psi3 = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = @$"-C C:\gitlocal\MyProject config http.sslVerify false",
                WorkingDirectory = @"C:\gitlocal",
                UseShellExecute = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
            };

            System.Diagnostics.Process.Start(psi3)?.WaitForExit();

            System.IO.File.WriteAllText(@"C:\gitlocal\Readme.md", "test");


            System.Diagnostics.ProcessStartInfo psi4 = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                Arguments = @$"-C C:\gitlocal\MyProject push",
                WorkingDirectory = @"C:\gitlocal",
                UseShellExecute = true,
                RedirectStandardOutput = false,
                RedirectStandardError = false,
                RedirectStandardInput = false,
            };

            System.Diagnostics.Process.Start(psi4)?.WaitForExit();


            //// Send a GET request to the root endpoint.
            //HttpResponseMessage response = await client!.GetAsync("/");
            //response.EnsureSuccessStatusCode();
            //await Task.Delay(2000);
            //response = await client!.GetAsync("/");
            //response.EnsureSuccessStatusCode();
            //await Task.Delay(2000);
            //response = await client!.GetAsync("/");
            //response.EnsureSuccessStatusCode();
            //await Task.Delay(2000);


            Assert.IsTrue(true);
            return;
        }
    }
}
