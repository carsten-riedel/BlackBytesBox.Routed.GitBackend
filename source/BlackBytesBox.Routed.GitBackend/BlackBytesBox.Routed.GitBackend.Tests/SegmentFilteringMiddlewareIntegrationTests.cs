using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using BlackBytesBox.Routed.GitBackend.Extensions.IApplicationBuilderExtensions;
using BlackBytesBox.Routed.GitBackend.Extensions.IServiceCollectionExtensions;
using BlackBytesBox.Routed.GitBackend.Middleware;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BlackBytesBox.Routed.GitBackend.Tests
{
    [TestClass]
    public sealed class SegmentFilteringMiddlewareIntegrationTests
    {
        private static WebApplicationBuilder? builder;
        private static WebApplication? app;
        private HttpClient? client;

        [ClassInitialize]
        public static async Task ClassInit(TestContext context)
        {
            // Create the builder using the minimal hosting model.
            builder = WebApplication.CreateBuilder();

            // Set a fixed URL for the host.
            builder.WebHost.UseUrls("https://localhost:5426");
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);

            builder.Services.AddSegmentFilteringMiddleware(builder.Configuration);
            builder.Services.AddFailurePointsFilteringMiddleware(builder.Configuration);

            // Build the application.
            app = builder.Build();

            app.UseSegmentFilteringMiddleware(); 
            app.UseFailurePointsFilteringMiddleware();

            // Define an array of URL patterns.
            var urls = new[] { "/", "/home/fooa" , ".git" , "/home", "/index", "/welcome" };

            // Map each URL pattern to the same GET endpoint handler.
            foreach (var url in urls)
            {
                app.MapGet(url, async context =>
                {
                    await context.Response.WriteAsync("Hello, world!");
                });
            }

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
                BaseAddress = new Uri("https://localhost:5426"),
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
            HttpResponseMessage response = await client!.GetAsync("/home");
            response.EnsureSuccessStatusCode();
            await Task.Delay(2000);
            response = await client!.GetAsync("/home/fooa");
            response.EnsureSuccessStatusCode();
            await Task.Delay(2000);
            response = await client!.GetAsync("/");
            response.EnsureSuccessStatusCode();
            await Task.Delay(2000);
            response = await client!.GetAsync("");
            response.EnsureSuccessStatusCode();
            await Task.Delay(2000);


            Assert.IsTrue(true);
            return;
        }
    }
}
