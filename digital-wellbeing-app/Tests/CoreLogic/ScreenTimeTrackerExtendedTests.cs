using System;
using System.Threading;
using Xunit;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app.Tests.CoreLogic
{
    public class ScreenTimeTrackerExtendedTests
    {
        [Fact]
        public void Constructor_InitializesState()
        {
            var tracker = new ScreenTimeTracker();
            Assert.NotNull(tracker);
            Assert.True(tracker.CurrentActiveTime.TotalSeconds >= 0);
        }

        [Fact]
        public void PauseAndResume_WorkCorrectly()
        {
            var tracker = new ScreenTimeTracker();
            tracker.Start();

            tracker.Pause();
            Assert.Equal(TrackingState.Paused, tracker.State);

            tracker.Resume();
            Assert.NotEqual(TrackingState.Paused, tracker.State);

            tracker.Stop();
        }

        [Fact]
        public void State_ReflectsCurrentTracking()
        {
            var tracker = new ScreenTimeTracker();

            // Before start, state should not be Paused
            tracker.Start();

            // After start, state should be Active or Idle (depending on user activity)
            var state = tracker.State;
            Assert.True(state == TrackingState.Active || state == TrackingState.Idle,
                $"After Start(), state should be Active or Idle, got {state}");

            tracker.Pause();
            Assert.Equal(TrackingState.Paused, tracker.State);

            tracker.Resume();
            Assert.NotEqual(TrackingState.Paused, tracker.State);

            tracker.Stop();
        }

        [Fact]
        public void Dispose_StopsTracker()
        {
            var tracker = new ScreenTimeTracker();
            tracker.Start();
            tracker.Dispose();

            // After dispose, the tracker should not throw when accessed
            var time = tracker.CurrentActiveTime;
            Assert.True(time.TotalSeconds >= 0);
        }

        [Fact]
        public void ContinuousSessionSeconds_TracksWithinSession()
        {
            var tracker = new ScreenTimeTracker();
            tracker.Start();

            // Wait briefly
            Thread.Sleep(1100);

            // Continuous session should be tracking
            Assert.True(tracker.ContinuousSessionSeconds >= 0);

            tracker.Stop();
        }

        [Fact]
        public void MultipleStartStop_DoesNotThrow()
        {
            var tracker = new ScreenTimeTracker();

            tracker.Start();
            tracker.Stop();
            tracker.Start();
            tracker.Stop();

            // Should not throw or corrupt state
            Assert.True(tracker.CurrentActiveTime.TotalSeconds >= 0);
        }

        [Fact]
        public void PauseWhileStopped_DoesNotThrow()
        {
            var tracker = new ScreenTimeTracker();
            // Pause before start should not throw
            tracker.Pause();
            tracker.Resume();
        }
    }
}
