using Xunit;
using digital_wellbeing_app.Platform.Windows;

namespace digital_wellbeing_app.Tests.Platform
{
    public class BrowserTabInspectorTests
    {
        [Theory]
        [InlineData("chrome", true)]
        [InlineData("msedge", true)]
        [InlineData("firefox", true)]
        [InlineData("brave", true)]
        [InlineData("opera", true)]
        [InlineData("vivaldi", true)]
        [InlineData("CHROME", true)] // case-insensitive
        [InlineData("notepad", false)]
        [InlineData("explorer", false)]
        public void IsKnownBrowser_MatchesExpectedSet(string processName, bool expected)
        {
            Assert.Equal(expected, BrowserTabInspector.IsKnownBrowser(processName));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void ExtractHostname_NullOrBlank_ReturnsNull(string? input)
        {
            Assert.Null(BrowserTabInspector.ExtractHostname(input));
        }

        [Fact]
        public void ExtractHostname_PlainHostname_NoScheme()
        {
            // Chrome's omnibox omits the scheme for https pages (confirmed via live spike).
            Assert.Equal("example.com", BrowserTabInspector.ExtractHostname("example.com/pulse-uia-spike-test"));
        }

        [Fact]
        public void ExtractHostname_FullUrlWithScheme_DiscardsPathAndQuery()
        {
            Assert.Equal("youtube.com", BrowserTabInspector.ExtractHostname("https://www.youtube.com/watch?v=dQw4w9WgXcQ&t=30s"));
        }

        [Fact]
        public void ExtractHostname_CollapsesCommonSubdomains()
        {
            Assert.Equal("google.com", BrowserTabInspector.ExtractHostname("https://mail.google.com/mail/u/0/"));
            Assert.Equal("google.com", BrowserTabInspector.ExtractHostname("https://docs.google.com/document/d/abc"));
        }

        [Fact]
        public void ExtractHostname_TwoLabelPublicSuffix_KeepsThreeLabels()
        {
            Assert.Equal("bbc.co.uk", BrowserTabInspector.ExtractHostname("https://www.bbc.co.uk/news/uk-12345"));
        }

        [Fact]
        public void ExtractHostname_UnknownSuffix_FallsBackToLastTwoLabels()
        {
            // ".io" isn't in the special two-label-suffix list, so this is the documented naive
            // fallback - not a full Public Suffix List implementation.
            Assert.Equal("example.io", BrowserTabInspector.ExtractHostname("https://sub.example.io/page"));
        }

        [Theory]
        [InlineData("chrome://settings")]
        [InlineData("about:blank")]
        [InlineData("edge://history")]
        [InlineData(@"file:///C:/Users/test/report.html")]
        public void ExtractHostname_NonHttpScheme_ReturnsNull(string input)
        {
            Assert.Null(BrowserTabInspector.ExtractHostname(input));
        }

        [Fact]
        public void ExtractHostname_GarbageText_ReturnsNull()
        {
            Assert.Null(BrowserTabInspector.ExtractHostname("New Tab"));
        }

        [Fact]
        public void TryGetAddressBarText_UnknownProcess_ReturnsNull()
        {
            // No live window handle needed - unknown process names short-circuit before any UIA call.
            Assert.Null(BrowserTabInspector.TryGetAddressBarText(System.IntPtr.Zero, "notepad"));
        }
    }
}
