using Xunit;
using digital_wellbeing_app.Platform.Windows;

namespace digital_wellbeing_app.Tests.Platform
{
    /// <summary>
    /// Covers the 2026-07-17 fix consolidating "is audio playing" onto SoundExposureManager as the
    /// single source of truth, replacing a second, independent Core Audio COM query that could
    /// (and did) disagree with SoundMonitoringService's own device-aware tracking. In this test
    /// process there is no live System.Windows.Application, so IsPassivelyConsuming's audio check
    /// must degrade to false rather than throw - the same safe fallback every other
    /// (Application.Current as App)?.XyzProperty call site in this codebase relies on.
    /// </summary>
    public class ActivityDetectorTests
    {
        [Fact]
        public void IsPassivelyConsuming_NoLiveApp_DoesNotThrow()
        {
            var exception = Record.Exception(() => ActivityDetector.IsPassivelyConsuming());
            Assert.Null(exception);
        }

        [Fact]
        public void IsFullscreenAppActive_DoesNotThrow()
        {
            var exception = Record.Exception(() => ActivityDetector.IsFullscreenAppActive());
            Assert.Null(exception);
        }
    }
}
