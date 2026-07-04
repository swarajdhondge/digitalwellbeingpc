using System;
using System.Linq;
using Xunit;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>
    /// Tests for v2.2 Phase 1.6 crash recovery: an orphaned focus session (app crashed mid-session)
    /// must be ended at its last heartbeat, not at the fabricated full planned duration.
    /// Times are anchored to DateTime.Today so recovery (which queries today's sessions) always
    /// sees them regardless of the wall-clock time the test runs.
    /// </summary>
    public class FocusRecoveryTests
    {
        [Fact]
        public void RecoverOrphanedSessions_EndsAtLastHeartbeat_NotPlannedDuration()
        {
            DatabaseService.DeleteAllData();

            var start = DateTime.Today.AddHours(8);            // 08:00 today, a 60-min session
            var lastSeenUtc = start.AddMinutes(15).ToUniversalTime(); // last alive 08:15 (15 min in)
            DatabaseService.SaveFocusSession(new FocusSession
            {
                StartTime = start,
                PlannedDurationMinutes = 60,
                SessionDate = DateTime.Today.ToString("yyyy-MM-dd"),
                Completed = false,
                EndTime = null,                                // orphaned: never ended
                LastSeenUtc = lastSeenUtc
            });

            // Constructing the service runs RecoverOrphanedSessions().
            using var svc = new FocusSessionService();

            var recovered = DatabaseService.GetFocusSessionsForDate(DateTime.Today).Single();
            Assert.True(recovered.EndTime.HasValue);

            var expected = lastSeenUtc.ToLocalTime();          // ~08:15 today
            Assert.True(Math.Abs((recovered.EndTime!.Value - expected).TotalSeconds) < 5,
                $"Expected end ~{expected:o}, got {recovered.EndTime.Value:o}");

            // And crucially not the fabricated planned end (start + 60 min = 09:00).
            Assert.True(recovered.EndTime.Value < start.AddMinutes(60));
        }

        [Fact]
        public void RecoverOrphanedSessions_LegacyRowWithoutHeartbeat_FallsBackToPlanned()
        {
            DatabaseService.DeleteAllData();

            var start = DateTime.Today.AddHours(6);
            DatabaseService.SaveFocusSession(new FocusSession
            {
                StartTime = start,
                PlannedDurationMinutes = 45,
                SessionDate = DateTime.Today.ToString("yyyy-MM-dd"),
                Completed = false,
                EndTime = null,
                LastSeenUtc = DateTime.MinValue // legacy row, no heartbeat recorded
            });

            using var svc = new FocusSessionService();

            var recovered = DatabaseService.GetFocusSessionsForDate(DateTime.Today).Single();
            Assert.True(recovered.EndTime.HasValue);
            Assert.Equal(start.AddMinutes(45), recovered.EndTime!.Value, TimeSpan.FromSeconds(2));
        }
    }
}
