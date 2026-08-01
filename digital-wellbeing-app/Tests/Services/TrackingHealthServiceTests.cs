using System;
using System.Linq;
using Xunit;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;
using static digital_wellbeing_app.Services.TrackingHealthService;

namespace digital_wellbeing_app.Tests.Services
{
    public class TrackingHealthServiceTests : TestBase
    {
        [Fact]
        public void RecordHeartbeat_ThenGetHealthStatuses_ReportsHealthy()
        {
            TrackingHealthService.RecordHeartbeat("ScreenTimeTracker");

            var status = TrackingHealthService.GetHealthStatuses()
                .Single(s => s.TrackerName == "ScreenTimeTracker");

            Assert.Equal(HealthStatus.Healthy, status.Status);
            Assert.NotNull(status.LastSeenUtc);
            Assert.True(status.TickCount >= 1);
        }

        [Fact]
        public void GetHealthStatuses_NoHeartbeatRecorded_ReportsUnknown()
        {
            var status = TrackingHealthService.GetHealthStatuses()
                .Single(s => s.TrackerName == "AppUsageTracker");

            Assert.Equal(HealthStatus.Unknown, status.Status);
            Assert.Null(status.LastSeenUtc);
            Assert.Equal(0, status.TickCount);
        }

        [Fact]
        public void RecordHeartbeat_CalledTwice_IncrementsTickCount()
        {
            TrackingHealthService.RecordHeartbeat("SoundExposureManager");
            TrackingHealthService.RecordHeartbeat("SoundExposureManager");

            var status = TrackingHealthService.GetHealthStatuses()
                .Single(s => s.TrackerName == "SoundExposureManager");

            Assert.Equal(2, status.TickCount);
        }

        [Fact]
        public void RecordHeartbeat_WithError_SetsLastError()
        {
            TrackingHealthService.RecordHeartbeat("ScreenTimeTracker", "simulated failure");

            var status = TrackingHealthService.GetHealthStatuses()
                .Single(s => s.TrackerName == "ScreenTimeTracker");

            Assert.Equal("simulated failure", status.LastError);
        }

        [Fact]
        public void GetHealthStatuses_StaleHeartbeat_ReportsStale()
        {
            DatabaseService.SaveTrackerHeartbeat(new TrackerHeartbeat
            {
                TrackerName = "AppUsageTracker",
                LastSeenUtc = DateTime.UtcNow.AddMinutes(-10), // between the 5m healthy and 15m down windows
                TickCount = 5
            });

            var status = TrackingHealthService.GetHealthStatuses()
                .Single(s => s.TrackerName == "AppUsageTracker");

            Assert.Equal(HealthStatus.Stale, status.Status);
        }

        [Fact]
        public void GetHealthStatuses_VeryOldHeartbeat_ReportsDown()
        {
            DatabaseService.SaveTrackerHeartbeat(new TrackerHeartbeat
            {
                TrackerName = "SoundExposureManager",
                LastSeenUtc = DateTime.UtcNow.AddHours(-2),
                TickCount = 5
            });

            var status = TrackingHealthService.GetHealthStatuses()
                .Single(s => s.TrackerName == "SoundExposureManager");

            Assert.Equal(HealthStatus.Down, status.Status);
        }

        [Fact]
        public void GetHealthStatuses_ReturnsAllThreeKnownTrackers()
        {
            var statuses = TrackingHealthService.GetHealthStatuses();
            Assert.Equal(3, statuses.Count);
            Assert.Contains(statuses, s => s.TrackerName == "ScreenTimeTracker");
            Assert.Contains(statuses, s => s.TrackerName == "AppUsageTracker");
            Assert.Contains(statuses, s => s.TrackerName == "SoundExposureManager");
        }
    }
}
