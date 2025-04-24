using System;
using System.Threading;
using Xunit;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app.Tests.ScreenTime
{
    public class ScreenTimeTrackerTests
    {
        [Fact]
        public void Start_Accumulates_Time()
        {
            // Arrange
            var tracker = new ScreenTimeTracker();
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
        public void SessionStartTime_Should_BeSetToConstructorTime()
        {
            // Arrange
            var before = DateTime.Now;
            var tracker = new ScreenTimeTracker();
            var after = DateTime.Now;

            // Act
            var start = tracker.SessionStartTime;

            // Assert
            Assert.True(start >= before && start <= after,
                $"SessionStartTime {start} should be between {before} and {after}");
        }
    }
}
