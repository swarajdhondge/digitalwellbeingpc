#nullable enable
using Xunit;
using digital_wellbeing_app.Helpers;

namespace digital_wellbeing_app.Tests.Helpers
{
    public class AppIdentityTests
    {
        [Theory]
        [InlineData("chrome", "chrome")]
        [InlineData("Chrome", "chrome")]
        [InlineData("chrome.exe", "chrome")]
        [InlineData("Chrome.EXE", "chrome")]
        [InlineData(@"C:\Program Files\Google\Chrome\Application\chrome.exe", "chrome")]
        [InlineData(@"C:/Apps/VLC/vlc.exe", "vlc")]
        [InlineData("  devenv  ", "devenv")]
        public void NormalizeKey_CollapsesAllShapesToProcessName(string input, string expected)
        {
            Assert.Equal(expected, AppIdentity.NormalizeKey(input));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void NormalizeKey_BlankInput_ReturnsEmpty(string? input)
        {
            Assert.Equal(string.Empty, AppIdentity.NormalizeKey(input));
        }

        [Fact]
        public void NormalizeKey_IsIdempotent()
        {
            var once = AppIdentity.NormalizeKey(@"C:\X\Foo.exe");
            Assert.Equal(once, AppIdentity.NormalizeKey(once));
        }

        [Fact]
        public void NormalizeKey_ProcessNameAndFullPath_ResolveEqual()
        {
            Assert.Equal(
                AppIdentity.NormalizeKey("chrome"),
                AppIdentity.NormalizeKey(@"C:\Program Files\Google\Chrome\Application\chrome.exe"));
        }

        [Fact]
        public void NormalizeKey_TwoArg_PrefersPath_FallsBackToName()
        {
            Assert.Equal("vlc", AppIdentity.NormalizeKey(@"C:\VLC\vlc.exe", "somethingElse"));
            Assert.Equal("notepad", AppIdentity.NormalizeKey("", "Notepad"));
            Assert.Equal("notepad", AppIdentity.NormalizeKey(null, "notepad.exe"));
        }
    }
}
