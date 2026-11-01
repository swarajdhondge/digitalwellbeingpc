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

        // ---- Evaluate (one timer tick), driven without the real timer or OS idle state ----

        private static BreakReminderService CreateEnabledService(int intervalMinutes = 20)
            => new BreakReminderService { IsEnabled = true, IntervalMinutes = intervalMinutes };

        [Fact]
        public void Evaluate_ActiveBeforeInterval_DoesNotFire()
        {
            var svc = CreateEnabledService();
            int fired = 0;
            svc.BreakDue += () => fired++;

            svc.Evaluate(DateTime.Now.AddMinutes(19), userIdle: false);

            Assert.Equal(0, fired);
            Assert.False(svc.IsBreakPending);
        }

        [Fact]
        public void Evaluate_ActiveAfterInterval_FiresOnceAndStaysPending()
        {
            var svc = CreateEnabledService();
            int fired = 0;
            svc.BreakDue += () => fired++;

            svc.Evaluate(DateTime.Now.AddMinutes(21), userIdle: false);
            svc.Evaluate(DateTime.Now.AddMinutes(22), userIdle: false);

            Assert.Equal(1, fired);
            Assert.True(svc.IsBreakPending);
        }

        [Fact]
        public void Evaluate_IdleWhilePending_ResolvesTheBreak()
        {
            // Issue #60: a reminder the user never saw must not block every later reminder.
            var svc = CreateEnabledService();
            int dismissed = 0;
            svc.BreakDismissed += () => dismissed++;
            svc.Evaluate(DateTime.Now.AddMinutes(21), userIdle: false);
            Assert.True(svc.IsBreakPending);

            svc.Evaluate(DateTime.Now.AddMinutes(30), userIdle: true);

            Assert.False(svc.IsBreakPending);
            Assert.Equal(1, dismissed);
            Assert.Equal(0, svc.SnoozeCount);
        }

        [Fact]
        public void Evaluate_AfterIdleResolvesBreak_FiresAgainAfterNextInterval()
        {
            var svc = CreateEnabledService();
            int fired = 0;
            svc.BreakDue += () => fired++;
            var start = DateTime.Now;

            svc.Evaluate(start.AddMinutes(21), userIdle: false);  // fires, pending
            svc.Evaluate(start.AddMinutes(30), userIdle: true);   // away -> resolved, timer restarts
            svc.Evaluate(start.AddMinutes(45), userIdle: false);  // 15 min since restart -> not yet
            Assert.Equal(1, fired);

            svc.Evaluate(start.AddMinutes(51), userIdle: false);  // 21 min since restart -> fires
            Assert.Equal(2, fired);
        }

        [Fact]
        public void Evaluate_IdleWithNothingPending_DoesNotRaiseDismissed()
        {
            var svc = CreateEnabledService();
            int dismissed = 0;
            svc.BreakDismissed += () => dismissed++;

            svc.Evaluate(DateTime.Now.AddMinutes(10), userIdle: true);

            Assert.Equal(0, dismissed);
        }

        [Fact]
        public void Evaluate_WhenDisabled_DoesNothing()
        {
            var svc = CreateEnabledService();
            svc.IsEnabled = false;
            int fired = 0;
            svc.BreakDue += () => fired++;

            svc.Evaluate(DateTime.Now.AddMinutes(60), userIdle: false);

            Assert.Equal(0, fired);
        }
    }
}
