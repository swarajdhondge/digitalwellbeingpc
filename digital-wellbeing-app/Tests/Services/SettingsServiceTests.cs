using System;
using System.IO;
using Xunit;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>
    /// Settings tests. Note: these share a real settings file, so tests that
    /// modify values use the same SettingsService instance for save+load
    /// to avoid race conditions with parallel test execution.
    /// </summary>
    public class SettingsServiceTests
    {
        [Fact]
        public void LoadHarmfulThreshold_ReturnsReasonableValue()
        {
            var svc = new SettingsService();
            var threshold = svc.LoadHarmfulThreshold();
            Assert.True(threshold > 0 && threshold <= 120,
                $"Harmful threshold {threshold} should be in a reasonable range (0-120 dB)");
        }

        [Fact]
        public void SaveAndLoadBreakReminderInterval_RoundTrips()
        {
            var svc = new SettingsService();
            var original = svc.LoadBreakReminderInterval();

            svc.SaveBreakReminderInterval(45);
            Assert.Equal(45, svc.LoadBreakReminderInterval());

            // Restore original
            svc.SaveBreakReminderInterval(original);
        }

        [Fact]
        public void SaveAndLoadWindowState_RoundTrips()
        {
            var svc = new SettingsService();
            svc.SaveWindowState(100.5, 200.5, 1100, 750, false);

            // Load from same instance (in-memory verification)
            var state = svc.LoadWindowState();
            Assert.NotNull(state);
            Assert.Equal(100.5, state.Value.Left);
            Assert.Equal(200.5, state.Value.Top);
            Assert.Equal(1100, state.Value.Width);
            Assert.Equal(750, state.Value.Height);
            Assert.False(state.Value.IsMaximized);
        }

        [Fact]
        public void SaveAndLoadWindowState_Maximized()
        {
            var svc = new SettingsService();
            svc.SaveWindowState(50, 50, 800, 600, true);

            var state = svc.LoadWindowState();
            Assert.NotNull(state);
            Assert.True(state.Value.IsMaximized);
        }

        [Fact]
        public void SaveAndLoadWindDownSettings_RoundTrips()
        {
            var svc = new SettingsService();
            svc.SaveWindDownEnabled(true);
            svc.SaveWindDownStartTime(22, 30);
            svc.SaveWindDownEndTime(6, 0);

            Assert.True(svc.LoadWindDownEnabled());
            Assert.Equal(22, svc.LoadWindDownStartHour());
            Assert.Equal(30, svc.LoadWindDownStartMinute());
            Assert.Equal(6, svc.LoadWindDownEndHour());
            Assert.Equal(0, svc.LoadWindDownEndMinute());

            // Restore defaults
            svc.SaveWindDownEnabled(false);
        }

        [Fact]
        public void SaveAndLoadBreakReminderEnabled_RoundTrips()
        {
            var svc = new SettingsService();
            svc.SaveBreakReminderEnabled(true);
            Assert.True(svc.LoadBreakReminderEnabled());

            svc.SaveBreakReminderEnabled(false);
            Assert.False(svc.LoadBreakReminderEnabled());
        }

        [Fact]
        public void LoadCloseToTray_DefaultsToTrue()
        {
            // Fresh settings should keep Pulse running in the tray so tracking never
            // silently stops when the window is closed.
            SettingsService.FolderOverride = Path.Combine(Path.GetTempPath(), "PulseTest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var svc = new SettingsService();
                Assert.True(svc.LoadCloseToTray());
            }
            finally
            {
                SettingsService.FolderOverride = null;
            }
        }

        [Fact]
        public void SaveAndLoadCloseToTray_RoundTrips()
        {
            var svc = new SettingsService();
            var original = svc.LoadCloseToTray();

            svc.SaveCloseToTray(false);
            Assert.False(svc.LoadCloseToTray());

            svc.SaveCloseToTray(true);
            Assert.True(svc.LoadCloseToTray());

            // Restore original
            svc.SaveCloseToTray(original);
        }

        [Fact]
        public void SaveAndLoadCloseToTrayHintShown_RoundTrips()
        {
            var svc = new SettingsService();
            var original = svc.LoadCloseToTrayHintShown();

            svc.SaveCloseToTrayHintShown(true);
            Assert.True(svc.LoadCloseToTrayHintShown());

            // Restore original
            svc.SaveCloseToTrayHintShown(original);
        }

        [Fact]
        public void PersistenceSurvivesNewInstance()
        {
            // This test verifies that data persists to disk.
            // Uses FirstRunCompleted - a simple bool that is unlikely to conflict.
            var svc = new SettingsService();
            svc.SaveFirstRunCompleted(true);

            var svc2 = new SettingsService();
            Assert.True(svc2.LoadFirstRunCompleted(), "FirstRunCompleted should persist across instances");
        }

        [Fact]
        public void SaveAndLoadDeviceTypeOverride_RoundTrips()
        {
            var svc = new SettingsService();
            svc.SaveDeviceTypeOverride("Headphones");
            Assert.Equal("Headphones", svc.LoadDeviceTypeOverride());

            // Restore to auto-detect
            svc.SaveDeviceTypeOverride(null);
            Assert.Null(svc.LoadDeviceTypeOverride());
        }

        [Fact]
        public void SaveDeviceTypeOverride_EmptyString_LoadsAsNull()
        {
            var svc = new SettingsService();
            svc.SaveDeviceTypeOverride("");
            Assert.Null(svc.LoadDeviceTypeOverride());
        }

        [Fact]
        public void RetentionMonths_DefaultsTo12_AndRoundTrips()
        {
            var settings = new SettingsService();
            Assert.Equal(12, new SettingsService().LoadRetentionMonths());

            settings.SaveRetentionMonths(6);
            Assert.Equal(6, new SettingsService().LoadRetentionMonths());

            settings.SaveRetentionMonths(12); // restore default
        }
    }
}
