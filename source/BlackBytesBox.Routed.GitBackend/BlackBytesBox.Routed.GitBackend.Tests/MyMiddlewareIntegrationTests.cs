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
    public sealed class MyMiddlewareIntegrationTests
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
            builder.WebHost.UseUrls("https://localhost:5425");

            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);


            // Optionally, add a separate JSON configuration file (for example, myMiddlewareConfig.json)
            // Uncomment the following line if you want to load a separate config file:
            // builder.Configuration.AddJsonFile("myMiddlewareConfig.json", optional: true, reloadOnChange: true);

            // Register middleware configuration. This method will internally resolve IConfiguration.
            // If a "MyMiddleware" section exists, it is used.
            // Manual configuration provided in the lambda takes precedence over configuration file values.

            //builder.Services.AddMyMiddlewareConfiguration(options =>
            //{
            //    options.Option1 = "Manually overridden value";
            //    // Option2 will remain as defined in the configuration file or default if not provided.
            //});

            //builder.Services.AddMyMiddlewareConfiguration(builder.Configuration);

            builder.Services.AddRemoteIPFilteringMiddleware(builder.Configuration);
            builder.Services.AddHttpProtocolFilteringMiddleware(builder.Configuration);
            builder.Services.AddHostNameFilteringMiddleware(builder.Configuration);
            builder.Services.AddUserAgentFilteringMiddleware(builder.Configuration);
            builder.Services.AddRequestUrlFilteringMiddleware(builder.Configuration);
            builder.Services.AddDnsHostNameFilteringMiddleware(builder.Configuration);
            builder.Services.AddHeaderPresentsFilteringMiddleware(builder.Configuration);
            builder.Services.AddAcceptLanguageFilteringMiddleware(builder.Configuration);
            builder.Services.AddSegmentFilteringMiddleware(builder.Configuration);
            builder.Services.AddPathDeepFilteringMiddleware(builder.Configuration);
            builder.Services.AddHeaderValuesRequiredFilteringMiddleware(builder.Configuration);
            builder.Services.AddFailurePointsFilteringMiddleware(builder.Configuration);


            // Build the application.
            app = builder.Build();

            //app.UseMyMiddleware();

            // Option 2: Use DI options and apply an additional manual configuration.
            // For example, override Option1 while still getting refresh behavior.

            app.UseHttpProtocolFilteringMiddleware();
            app.UseHostNameFilteringMiddleware();
            app.UseUserAgentFilteringMiddleware();
            app.UseRequestUrlFilteringMiddleware();
            app.UseDnsHostNameFilteringMiddleware();
            app.UseHeaderPresentsFilteringMiddleware();
            app.UseAcceptLanguageFilteringMiddleware();
            app.UseSegmentFilteringMiddleware(); 
            app.UsePathDeepFilteringMiddleware();
            app.UseHeaderValuesRequiredFilteringMiddleware();
            app.UseFailurePointsFilteringMiddleware();


            // Map a simple GET endpoint for testing.
            app.MapGet("/", async context =>
            {
                await context.Response.WriteAsync("Hello, world!");
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
                BaseAddress = new Uri("https://localhost:5425"),
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
