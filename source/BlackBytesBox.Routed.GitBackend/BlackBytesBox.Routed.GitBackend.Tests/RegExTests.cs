using System.Threading.Tasks;

using BlackBytesBox.Routed.GitBackend.Extensions.StringExtensions;
using BlackBytesBox.Routed.GitBackend.Utility.RegexUtility;
using BlackBytesBox.Routed.GitBackend.Utility.StringUtility;

using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BlackBytesBox.Routed.GitBackend.Tests
{
    [TestClass]
    public sealed class RegExTests
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
        [DataRow("foo", "*", true)]
        [DataRow("bar", "b?r", true)]
        [DataRow(null, null, true)]
        [DataRow("foo", null, false)]
        [DataRow(null, "*", false)]
        [DataRow("ydyn", "*dyn*", true)]
        [DataRow("dynx", "*dyn*", true)]
        public async Task IsRegExMatchIntegration(string? testString, string? pattern, bool expected)
        {
            // Act: Call the method under test.
            bool result = RegexUtility.IsRegexMatch(testString, pattern, System.Text.RegularExpressions.RegexOptions.None);

            // Assert: Validate that the result matches the expected value.
            Assert.AreEqual(expected, result, $"Failed for input: '{testString}', pattern: '{pattern}'");

            await Task.CompletedTask;
        }

        [TestMethod]
        [DataRow("foo", new string[0], false)]
        [DataRow("foo", new string?[0], false)]
        [DataRow("foo", null, false)]
        [DataRow("foo", new string[] {}, false)]
        [DataRow(null, new string[] { null, "", "*", "test" }, true)]
        [DataRow(null, new string?[] { null, "" , "*" , "test" }, true)]
        [DataRow("", new string?[] { null, "", "*", "test" }, true)]
        [DataRow("foo", new string?[]{"*f*" , "*o" , "o"}, true)]
        [DataRow("CRITFORGE.COM", new string?[] { "critforge.com", }, true)]
        public async Task IsRegExMatchIntegrationArray(string? testString, string?[]? pattern, bool expected)
        {


            StringUtility.PatternMatchResult result = StringUtility.MatchesAnyPattern(testString, pattern, true);
            Assert.AreEqual(expected, result.IsMatch, $"Failed for input: '{testString}'");

            await Task.CompletedTask;
        }
    }
}
