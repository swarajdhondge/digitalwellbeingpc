using System;
using System.Threading;
using Xunit;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    public class BreakReminderServiceTests
    {
        private BreakReminderService CreateTestService()
        {
            var svc = new BreakReminderService();
            svc.TestMode = true;
            svc.IsEnabled = true;
            svc.IntervalMinutes = 1; // Short interval for testing
            return svc;
        }

        [Fact]
        public void Constructor_SetsDefaults()
        {
            var svc = new BreakReminderService();
            Assert.False(svc.IsEnabled);
            Assert.Equal(20, svc.IntervalMinutes);
            Assert.Equal(3, svc.MaxSnoozeCount);
            Assert.True(svc.SoundEnabled);
            Assert.False(svc.IsBreakPending);
        }

        [Fact]
        public void Start_WhenEnabled_DoesNotThrow()
        {
            var svc = CreateTestService();
            svc.Start();
            svc.Stop();
        }

        [Fact]
        public void Snooze_IncrementsCount()
        {
            var svc = CreateTestService();
            svc.Start();

            // Manually trigger a break state
            bool breakFired = false;
            svc.BreakDue += () => breakFired = true;

            // Snooze should work
            svc.Snooze(5);
            Assert.Equal(1, svc.SnoozeCount);

            svc.Snooze(5);
            Assert.Equal(2, svc.SnoozeCount);

            svc.Stop();
        }

        [Fact]
        public void Snooze_ReturnsTrue_WhenUnderMax()
        {
            var svc = CreateTestService();
            svc.MaxSnoozeCount = 2;
            svc.Start();

            Assert.True(svc.Snooze(5));
            Assert.True(svc.Snooze(5));
            Assert.False(svc.Snooze(5)); // Should fail - max reached

            svc.Stop();
        }

        [Fact]
        public void CanSnooze_ReflectsSnoozeAvailability()
        {
            var svc = CreateTestService();
            svc.MaxSnoozeCount = 1;
            svc.Start();

            Assert.True(svc.CanSnooze);
            svc.Snooze(5);
            Assert.False(svc.CanSnooze);

            svc.Stop();
        }

        [Fact]
        public void Dismiss_ResetsBreakState()
        {
            var svc = CreateTestService();
            svc.Start();

            bool dismissed = false;
            svc.BreakDismissed += () => dismissed = true;

            svc.Dismiss();
            Assert.True(dismissed);
            Assert.False(svc.IsBreakPending);

            svc.Stop();
        }

        [Fact]
        public void Dismiss_ResetsSnoozeCount()
        {
            var svc = CreateTestService();
            svc.Start();

            svc.Snooze(5);
            svc.Snooze(5);
            Assert.Equal(2, svc.SnoozeCount);

            svc.Dismiss();
            Assert.Equal(0, svc.SnoozeCount);

            svc.Stop();
        }

        [Fact]
        public void TimeUntilNextBreak_IsPositive_WhenRunning()
        {
            var svc = CreateTestService();
            svc.IntervalMinutes = 20;
            svc.Start();

            // Should have some time remaining
            Assert.True(svc.TimeUntilNextBreak.TotalSeconds > 0,
                $"TimeUntilNextBreak should be positive, got {svc.TimeUntilNextBreak}");

            svc.Stop();
        }

        [Fact]
        public void SaveAndLoadSettings_Persists()
        {
            var svc = new BreakReminderService();
            svc.IsEnabled = true;
            svc.IntervalMinutes = 30;
            svc.SoundEnabled = false;
            svc.MaxSnoozeCount = 5;
            svc.SaveSettings();

            var svc2 = new BreakReminderService();
            svc2.LoadSettings();
            Assert.True(svc2.IsEnabled);
            Assert.Equal(30, svc2.IntervalMinutes);
            Assert.False(svc2.SoundEnabled);

            // Restore defaults
            svc.IsEnabled = false;
            svc.IntervalMinutes = 20;
            svc.SoundEnabled = true;
            svc.MaxSnoozeCount = 3;
            svc.SaveSettings();
        }

        [Fact]
        public void Stop_PreventsBreakFiring()
        {
            var svc = CreateTestService();
            bool breakFired = false;
            svc.BreakDue += () => breakFired = true;

            svc.Start();
            svc.Stop();

            // After stop, no break should fire
            Thread.Sleep(200);
            Assert.False(breakFired);
        }
    }
}
