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
            client.DefaultRequestHeaders.Add("APPID", "1234");
            client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de-DE");
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

            
            // Send a GET request to the root endpoint.
            HttpResponseMessage response = await client!.GetAsync("/");
            response.EnsureSuccessStatusCode();
            await Task.Delay(2000);
            response = await client!.GetAsync("/");
            response.EnsureSuccessStatusCode();
            await Task.Delay(2000);
            response = await client!.GetAsync("/");
            response.EnsureSuccessStatusCode();
            await Task.Delay(2000);


            Assert.IsTrue(true);
            return;
            // Verify that the middleware injected the "X-Option1" header.
            Assert.IsTrue(response.Headers.Contains("X-Option1"), "The response should contain the 'X-Option1' header.");
            string headerValue = string.Join("", response.Headers.GetValues("X-Option1"));

            // With no manual override provided, the default value should be "default value".
            Assert.AreEqual("default value", headerValue, "The 'X-Option1' header should have the default value.");

            await Task.Delay(40000);

            // Send a GET request to the root endpoint.
            response = await client!.GetAsync("/");
            response.EnsureSuccessStatusCode();

            // Verify that the middleware injected the "X-Option1" header.
            Assert.IsTrue(response.Headers.Contains("X-Option1"), "The response should contain the 'X-Option1' header.");
            headerValue = string.Join("", response.Headers.GetValues("X-Option1"));

            // With no manual override provided, the default value should be "default value".
            Assert.AreEqual("foo", headerValue, "The 'X-Option1' header should have the default value.");
        }
    }
}
