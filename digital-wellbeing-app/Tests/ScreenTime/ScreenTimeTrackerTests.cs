using System;
using System.Threading;
using Xunit;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app.Tests.ScreenTime
{
    public class ScreenTimeTrackerTests : TestBase
    {
        [Fact]
        public void Start_Accumulates_Time()
        {
            // Arrange
            var tracker = new ScreenTimeTracker();
            // Force the "active" path regardless of real machine input, so accumulation is
            // deterministic on idle dev machines and headless CI.
            tracker.IdleTimeProvider = () => TimeSpan.Zero;
            var beforeStartTime = tracker.CurrentActiveTime; // likely zero on fresh day

            // Act
            tracker.Start();
            // Simulate user activity for 2 seconds
            Thread.Sleep(2000);
            tracker.Stop();
            var afterStopTime = tracker.CurrentActiveTime;

            // Assert
            Assert.True(afterStopTime > beforeStartTime, "Active time should increase after Start/Stop.");
        }

        [Fact]
        public void SessionStartTime_Should_BeSetToToday()
        {
            // Arrange
            var tracker = new ScreenTimeTracker();

            // Act
            var start = tracker.SessionStartTime;

            // Assert - SessionStartTime should be today (may be loaded from DB or set fresh)
            Assert.Equal(DateTime.Now.Date, start.Date);
        }
    }
}
