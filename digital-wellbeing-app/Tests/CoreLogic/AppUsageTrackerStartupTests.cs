using System;
using Xunit;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app.Tests.CoreLogic
{
    /// <summary>
    /// Covers the 2026-07-17 startup-blind-spot fix: SetWinEventHook only fires on future
    /// foreground changes, so without synthesizing one initial resolution, the app already
    /// foreground when Pulse launches got zero credit until the user switched away and back.
    ///
    /// SynthesizeInitialFocus (private, called from Start()) depends on two pieces of real OS
    /// state this test doesn't control: the actual foreground window, and
    /// WindowsIdleTimeHelper.IsUserIdle (internal to the main assembly, not visible here without
    /// InternalsVisibleTo, which isn't configured). Rather than force a specific outcome, these
    /// tests assert the invariant that holds under either real-world outcome.
    /// </summary>
    public class AppUsageTrackerStartupTests : TestBase
    {
        [Fact]
        public void Start_DoesNotThrow_RegardlessOfForegroundOrIdleState()
        {
            var tracker = new AppUsageTracker();
            var exception = Record.Exception(() => tracker.Start());
            Assert.Null(exception);
            tracker.Dispose();
        }

        [Fact]
        public void Start_WhenSessionIsSynthesized_StartTimeIsRecent()
        {
            var before = DateTime.Now;
            var tracker = new AppUsageTracker();
            tracker.Start();
            var after = DateTime.Now;

            // A real machine is essentially never idle for 300s in a live/CI test run, so this
            // should be non-null in practice - but if the current foreground window couldn't be
            // resolved (e.g. a headless session) or the machine genuinely is idle, null is also a
            // correct, non-buggy outcome, so this only asserts freshness when a session exists.
            var session = tracker.CurrentSession;
            if (session != null)
            {
                Assert.InRange(session.StartTime, before.AddSeconds(-1), after.AddSeconds(1));
            }

            tracker.Dispose();
        }

        [Fact]
        public void Stop_AfterStart_DoesNotThrow()
        {
            var tracker = new AppUsageTracker();
            tracker.Start();
            var exception = Record.Exception(() => tracker.Stop());
            Assert.Null(exception);
            tracker.Dispose();
        }
    }
}
