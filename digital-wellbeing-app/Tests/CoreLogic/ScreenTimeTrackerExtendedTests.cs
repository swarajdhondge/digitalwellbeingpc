using System;
using System.Threading;
using System.Linq;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;
using Xunit;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app.Tests.CoreLogic
{
    public class ScreenTimeTrackerExtendedTests : TestBase
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

        [Fact]
        public void ReturningToPreviouslyRecordedDate_PreservesItsTotal()
        {
            var day = DateTime.Today;
            DatabaseService.SaveScreenTimePeriod(new ScreenTimePeriod
            {
                SessionDate = day.ToString("yyyy-MM-dd"), AccumulatedActiveSeconds = 3600,
                SessionStartTime = day.AddHours(9).ToString("o")
            });
            var now = day.AddHours(12);
            using var tracker = new ScreenTimeTracker { Clock = () => now };
            now = day.AddDays(1).AddHours(1);
            tracker.HandleTimeChanged();
            now = day.AddHours(13);
            tracker.HandleTimeChanged();
            Assert.Equal(TimeSpan.FromHours(1), tracker.CurrentActiveTime);
            Assert.Equal(3600, DatabaseService.GetScreenTimePeriodsForRange(day, day).Single().AccumulatedActiveSeconds);
        }

        [Fact]
        public void IdleThresholdBeforeMidnight_DoesNotCreditTheIdleGap()
        {
            var day = DateTime.Today;
            var now = day.AddHours(23).AddMinutes(59).AddSeconds(50);
            var idle = TimeSpan.FromSeconds(300);
            using var tracker = new ScreenTimeTracker
            {
                Clock = () => now, IdleTimeProvider = () => idle, PassiveConsumptionProvider = () => false
            };
            CallPrivate(tracker, "CheckActivity", null, null);
            var before = tracker.CurrentActiveTime.TotalSeconds;
            now = day.AddDays(1).AddSeconds(20);
            idle = TimeSpan.FromSeconds(330);
            CallPrivate(tracker, "CheckActivity", null, null);
            var prior = DatabaseService.GetScreenTimePeriodsForRange(day, day).Single();
            Assert.Equal((int)before, prior.AccumulatedActiveSeconds);
            Assert.Equal(TimeSpan.Zero, tracker.CurrentActiveTime);
        }

        [Fact]
        public void DelayedTimer_CountsElapsedTimeInsteadOfCallbacks()
        {
            var now = DateTime.Today.AddHours(10);
            using var tracker = new ScreenTimeTracker { Clock = () => now, IdleTimeProvider = () => TimeSpan.Zero };
            CallPrivate(tracker, "CheckActivity", null, null);
            var before = tracker.CurrentActiveTime;
            now = now.AddSeconds(10);
            CallPrivate(tracker, "CheckActivity", null, null);
            Assert.Equal(TimeSpan.FromSeconds(10), tracker.CurrentActiveTime - before);
            tracker.Pause();
            now = now.AddHours(1);
            before = tracker.CurrentActiveTime;
            CallPrivate(tracker, "CheckActivity", null, null);
            Assert.Equal(before, tracker.CurrentActiveTime);
        }

        [Theory]
        [InlineData(false, 10)]
        [InlineData(true, 20)]
        public void IdleTransition_StopsAtThresholdUnlessMediaIsPlaying(bool media, int expectedSeconds)
        {
            var now = DateTime.Today.AddHours(10);
            var idle = TimeSpan.Zero;
            using var tracker = new ScreenTimeTracker
            {
                Clock = () => now, IdleTimeProvider = () => idle, PassiveConsumptionProvider = () => media
            };
            CallPrivate(tracker, "CheckActivity", null, null);
            var before = tracker.CurrentActiveTime;
            now = now.AddSeconds(20);
            idle = TimeSpan.FromSeconds(310);
            CallPrivate(tracker, "CheckActivity", null, null);
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), tracker.CurrentActiveTime - before);
            Assert.Equal(media ? TrackingState.Active : TrackingState.Idle, tracker.State);
        }
    }
}
