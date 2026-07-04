using System;
using System.Threading;
using Xunit;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app.Tests.CoreLogic
{
    /// <summary>
    /// Tests for the v2.2 Phase 1.4 clock-change guard: on a system time change the trackers
    /// restart their current segment without losing the running day total.
    /// </summary>
    public class ClockChangeGuardTests
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
    }
}
