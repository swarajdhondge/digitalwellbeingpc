using System;
using System.Reflection;
using System.Threading;
using Xunit;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.CoreLogic
{
    /// <summary>
    /// Tests for the v2.2 Phase 1.4 clock-change guard: on a system time change the trackers
    /// restart their current segment without losing the running day total. Also covers the
    /// 2026-07-17 routine-path day-rollover guard (ordinary midnight rollover, no clock change).
    /// </summary>
    public class ClockChangeGuardTests : TestBase
    {
        [Fact]
        public void ScreenTracker_HandleTimeChanged_PreservesDayTotal_AndRestartsSegment()
        {
            var tracker = new ScreenTimeTracker { IdleTimeProvider = () => TimeSpan.Zero };
            tracker.Start();
            Thread.Sleep(1100); // accumulate ~1s of active time
            var before = tracker.CurrentActiveTime;

            tracker.HandleTimeChanged();

            var after = tracker.CurrentActiveTime;
            tracker.Stop();

            Assert.True(after >= before, "Day total must not be reset by a clock change.");
            Assert.NotNull(tracker.CurrentSessionStart); // a fresh segment was started
        }

        [Fact]
        public void AppTracker_FlushCurrentSession_NoActiveSession_DoesNotThrow()
        {
            var tracker = new AppUsageTracker();
            // No focus events have populated a current session; flushing must be a safe no-op.
            var ex = Record.Exception(() => tracker.FlushCurrentSession());
            Assert.Null(ex);
            tracker.Dispose();
        }

        /// <summary>
        /// The new routine-path guard (no clock-change event involved): a session left open
        /// overnight must still split at the day boundary. CheckDayRollover/OnPeriodicSave/
        /// OnAppChanged are all private and the only public path to populate _currentSession needs
        /// a live Windows focus-change event, so this seeds the field via reflection - the same
        /// private state a real overnight session would have left behind.
        /// </summary>
        [Fact]
        public void AppTracker_RoutineDayRollover_FlushesStaleOvernightSession()
        {
            var tracker = new AppUsageTracker();
            // DatabaseService has its own unrelated 24h MaxSessionSeconds write guard, which a
            // StartTime=yesterday/EndTime=now flush would sit right at the edge of - rather than
            // chase that edge with a clock this test doesn't control, this only asserts the
            // in-memory state transition (see below), which happens regardless of whether the DB
            // write of the *old* segment is accepted or rejected by that unrelated guard.
            var yesterday = DateTime.Now.AddDays(-1);

            var sessionField = typeof(AppUsageTracker).GetField("_currentSession", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var originalSession = new AppUsageSession
            {
                AppName = "overnight_test_app",
                ExecutablePath = "overnight_test_app.exe",
                StartTime = yesterday,
                WindowTitle = null
            };
            sessionField.SetValue(tracker, originalSession);

            var checkMethod = typeof(AppUsageTracker).GetMethod("CheckDayRollover", BindingFlags.NonPublic | BindingFlags.Instance)!;
            checkMethod.Invoke(tracker, null);

            // The stale in-memory segment must have been replaced with a fresh continuation
            // segment for the same app, starting today.
            var afterSession = (AppUsageSession?)sessionField.GetValue(tracker);
            Assert.NotNull(afterSession);
            Assert.NotSame(originalSession, afterSession);
            Assert.Equal("overnight_test_app", afterSession!.AppName);
            Assert.Equal(DateTime.Now.Date, afterSession.StartTime.Date);

            tracker.Dispose();
        }

        [Fact]
        public void AppTracker_CheckDayRollover_SameDaySession_DoesNotFlush()
        {
            var tracker = new AppUsageTracker();
            // Anchor to the calendar day rather than "five minutes ago" so this
            // remains a same-day test even when the suite runs across midnight.
            var todayStart = DateTime.Today;

            var sessionField = typeof(AppUsageTracker).GetField("_currentSession", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var originalSession = new AppUsageSession
            {
                AppName = "sameday_test_app",
                ExecutablePath = "sameday_test_app.exe",
                StartTime = todayStart,
                WindowTitle = null
            };
            sessionField.SetValue(tracker, originalSession);

            var checkMethod = typeof(AppUsageTracker).GetMethod("CheckDayRollover", BindingFlags.NonPublic | BindingFlags.Instance)!;
            checkMethod.Invoke(tracker, null);

            // Same in-memory instance, untouched - no rollover should have happened.
            var afterSession = (AppUsageSession?)sessionField.GetValue(tracker);
            Assert.Same(originalSession, afterSession);
            Assert.Equal(todayStart, afterSession!.StartTime);

            tracker.Dispose();
        }
    }
}
