using System;
using System.Threading.Tasks;

using BlackBytesBox.Routed.GitBackend.Utility.HttpContextUtility;
using BlackBytesBox.Routed.GitBackend.Utility.RegexUtility;
using BlackBytesBox.Routed.GitBackend.Utility.StringUtility;

using Microsoft.AspNetCore.Http;

namespace BlackBytesBox.Routed.GitBackend.Tests
{
    [TestClass]
    public sealed class ContextTests
    {
        [ClassInitialize]
        public static async Task ClassInit(TestContext context)
        {
            // Any class initialization logic goes here.
            await Task.CompletedTask;
        }

        [ClassCleanup]
        public static async Task ClassCleanup()
        {
            // Any class cleanup logic goes here.
            await Task.CompletedTask;
        }

        [TestInitialize]
        public void TestInit()
        {
            // Initialization before each test if needed.
        }

        [TestCleanup]
        public void TestCleanup()
        {
            // Cleanup after each test if needed.
        }

        [TestMethod]
        //[DataRow("https://www.google.com/")]
        [DataRow("https:///")]
        public async Task IsRegExMatchIntegration(string? testString)
        {
            // Arrange: Create a test HttpContext.
            var context = new DefaultHttpContext();

            if (!string.IsNullOrEmpty(testString))
            {
                // If the test string is a valid URI, use its components.
                if (Uri.TryCreate(testString, UriKind.Absolute, out var validUri))
                {
                    context.Request.Scheme = validUri.Scheme;

                    // Use HostString constructor overload based on whether the port is the default.
                    context.Request.Host = validUri.IsDefaultPort
                        ? new HostString(validUri.Host)
                        : new HostString(validUri.Host, validUri.Port);

                    context.Request.Path = validUri.AbsolutePath;
                    context.Request.QueryString = new QueryString(validUri.Query);
                }
                else
                {
                    // For an invalid URI (e.g. "https:///"), manually set properties so that GetDisplayUrl() returns testString.
                    // Assume the test string starts with a valid scheme (http or https).
                    if (testString.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Request.Scheme = "https";
                    }
                    else if (testString.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Request.Scheme = "http";
                    }
                    else
                    {
                        context.Request.Scheme = "http"; // fallback scheme
                    }

                    // Simulate an invalid URI by setting an empty host.
                    context.Request.Host = new HostString("/");
                    context.Request.PathBase = "";
                    // For this example, choose "/" so that the constructed URL becomes, e.g., "https:///".
                    context.Request.Path = "";
                    context.Request.QueryString = QueryString.Empty;
                }
            }

            // Act: Call the method under test.
            var result = HttpContextUtility.GetUriFromRequestDisplayUrl(context, null);

            // Assert:
            // If testString is valid, result should be non-null.
            // If testString is invalid, result should be null.
            if (Uri.TryCreate(testString, UriKind.Absolute, out _))
            {
                Assert.IsNotNull(result, $"Expected a valid URI for testString '{testString}', but got null.");
            }
            else
            {
                Assert.IsNull(result, $"Expected a null URI for invalid testString '{testString}', but got a URI.");
            }

            await Task.CompletedTask;
        }


    }
}
