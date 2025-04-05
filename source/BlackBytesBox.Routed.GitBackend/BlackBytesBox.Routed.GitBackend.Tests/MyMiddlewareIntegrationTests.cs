using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using BlackBytesBox.Routed.GitBackend.Middleware.GitBackendMiddleware;
using BlackBytesBox.Routed.GitBackend.Utility.ProcessUtility;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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

            var find = ProcessUtility.LocateExecutable("git");
            string oneup = Path.GetFullPath(find.Value.Directory + @"\..\mingw64\libexec\git-core\git-http-backend.exe");

            //var result = ProcessUtility.ExecuteProcess(@"git", @$"-C ""C:\gitremote"" init --bare MyProject.git", "");
            //var result2 = ProcessUtility.ExecuteProcess(@"git", @$"-C ""C:\gitremote\MyProject.git"" config http.receivepack true", "");


            builder.Services.AddBackendSettings2("BackendSettings.json");

            // Build the application.
            app = builder.Build();

            app.UseGitBackend2();

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

            var folderName = "MasterProject";
            var repoName = @$"{folderName}.git";

            var result = ProcessUtility.ExecuteProcess(@$"git", @$"-C C:\gitlocal -c http.sslVerify=false clone {localhostuser}/gitrepos/{repoName}", "");
            var result1 = ProcessUtility.ExecuteProcess(@$"git", @$"-C C:\gitlocal\{folderName} config http.sslVerify false", "");

            System.IO.File.WriteAllText(@$"C:\gitlocal\{folderName}\Readme.md", "test");

            var result2 = ProcessUtility.ExecuteProcess(@$"git", @$"-C C:\gitlocal\{folderName} add .", "");
            var result3 = ProcessUtility.ExecuteProcess(@$"git", @$"-C C:\gitlocal\{folderName} commit -m ""Initial commit""", "");
            var result4 = ProcessUtility.ExecuteProcess(@$"git", @$"-C C:\gitlocal\{folderName} push", "");


            Assert.IsTrue(true);
            return;

        }


    }
}
