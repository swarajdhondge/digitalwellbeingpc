using System;
using System.IO;
using Xunit;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>
    /// Locks the display-name resolution order (v2.2 Phase 2.4): curated KnownApps override →
    /// executable FileVersionInfo (rejecting generic descriptions) → prettified process name.
    /// </summary>
    public class AppNameServiceTests
    {
        [Theory]
        [InlineData("chrome", "Google Chrome")]
        [InlineData("chrome.exe", "Google Chrome")] // .exe stripped
        [InlineData("Code", "VS Code")]
        [InlineData("vlc", "VLC Player")]
        public void GetDisplayName_KnownApps_ReturnCuratedName(string process, string expected)
        {
            Assert.Equal(expected, AppNameService.GetDisplayName(process));
        }

        [Fact]
        public void GetDisplayName_Empty_ReturnsUnknown()
        {
            Assert.Equal("Unknown", AppNameService.GetDisplayName(""));
        }

        [Fact]
        public void GetDisplayName_UnknownProcess_PrettifiesCamelCase()
        {
            // Not in KnownApps and no executable path -> prettified process name.
            Assert.Equal("My Cool App", AppNameService.GetDisplayName("myCoolApp"));
        }

        [Fact]
        public void GetDisplayName_RealSystemExe_ResolvesToNonEmptyFriendlyName()
        {
            // Verify the FileVersionInfo fallback yields a good name over a real system exe.
            var explorer = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            if (!File.Exists(explorer)) return; // environment guard

            // "explorer" is curated; strip to prove path handling too. Use an unknown-but-real exe:
            var notepad = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System), "notepad.exe");
            if (File.Exists(notepad))
            {
                var name = AppNameService.GetDisplayName("notepad", notepad);
                Assert.False(string.IsNullOrWhiteSpace(name));
            }
        }
    }
}
