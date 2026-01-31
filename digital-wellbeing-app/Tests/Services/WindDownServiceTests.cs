using System;
using Xunit;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Tests.Services
{
    public class WindDownServiceTests
    {
        [Fact]
        public void Constructor_SetsDefaults()
        {
            var svc = new WindDownService();
            Assert.False(svc.IsEnabled);
            Assert.Equal(21, svc.StartHour);
            Assert.Equal(0, svc.StartMinute);
            Assert.Equal(7, svc.EndHour);
            Assert.Equal(0, svc.EndMinute);
            Assert.True(svc.ShowNotification);
            Assert.True(svc.ShowVisualCue);
        }

        [Fact]
        public void GetScheduleText_ReturnsFormattedString()
        {
            var svc = new WindDownService();
            svc.StartHour = 22;
            svc.StartMinute = 30;
            svc.EndHour = 6;
            svc.EndMinute = 0;

            var text = svc.GetScheduleText();
            Assert.False(string.IsNullOrWhiteSpace(text));
            // Should contain time references
            Assert.Contains(":", text);
        }

        [Fact]
        public void IsWindDownActive_DetectsCurrentState()
        {
            var svc = new WindDownService();
            svc.IsEnabled = true;

            // Set schedule to cover current time
            var now = DateTime.Now;
            svc.StartHour = now.Hour;
            svc.StartMinute = 0;
            svc.EndHour = (now.Hour + 2) % 24;
            svc.EndMinute = 0;

            // Should be active since we're within the window
            Assert.True(svc.IsWindDownActive);
        }

        [Fact]
        public void IsWindDownActive_FalseWhenDisabled()
        {
            var svc = new WindDownService();
            svc.IsEnabled = false;

            // Even if schedule covers now
            var now = DateTime.Now;
            svc.StartHour = now.Hour;
            svc.StartMinute = 0;
            svc.EndHour = (now.Hour + 2) % 24;
            svc.EndMinute = 0;

            Assert.False(svc.IsWindDownActive);
        }

        [Fact]
        public void IsWindDownActive_FalseOutsideWindow()
        {
            var svc = new WindDownService();
            svc.IsEnabled = true;

            // Set schedule to a time well away from now
            var now = DateTime.Now;
            // Set start to 12 hours from now
            svc.StartHour = (now.Hour + 12) % 24;
            svc.StartMinute = 0;
            svc.EndHour = (now.Hour + 14) % 24;
            svc.EndMinute = 0;

            Assert.False(svc.IsWindDownActive);
        }

        [Fact]
        public void SaveAndLoadSettings_Persists()
        {
            var svc = new WindDownService();
            svc.IsEnabled = true;
            svc.StartHour = 23;
            svc.StartMinute = 15;
            svc.EndHour = 5;
            svc.EndMinute = 45;
            svc.ShowNotification = false;
            svc.ShowVisualCue = false;
            svc.VisualStyle = WindDownVisualStyle.Purple;
            svc.VisualOpacity = 0.5;
            svc.SaveSettings();

            // Verify via same instance (in-memory state matches what was set)
            Assert.True(svc.IsEnabled);
            Assert.Equal(23, svc.StartHour);
            Assert.Equal(15, svc.StartMinute);
            Assert.Equal(5, svc.EndHour);
            Assert.Equal(45, svc.EndMinute);
            Assert.False(svc.ShowNotification);

            // Also verify a new instance loads correctly
            var svc2 = new WindDownService();
            svc2.LoadSettings();
            Assert.Equal(23, svc2.StartHour);

            // Restore defaults
            svc.IsEnabled = false;
            svc.SaveSettings();
        }

        [Fact]
        public void StartAndStop_DoNotThrow()
        {
            var svc = new WindDownService();
            svc.IsEnabled = true;
            svc.Start();
            svc.Stop();
        }

        [Fact]
        public void MarkNotificationShown_SetsFlag()
        {
            var svc = new WindDownService();
            Assert.False(svc.HasNotificationBeenShown);

            svc.MarkNotificationShown();
            Assert.True(svc.HasNotificationBeenShown);
        }
    }
}
