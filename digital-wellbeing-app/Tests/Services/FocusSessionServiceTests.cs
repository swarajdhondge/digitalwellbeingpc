using System;
using System.Linq;
using Xunit;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Tests.Services
{
    public class FocusSessionServiceTests : TestBase
    {
        [Fact]
        public void StartSession_EntersFocusMode()
        {
            var svc = new FocusSessionService();
            bool started = false;
            svc.SessionStarted += () => started = true;

            svc.StartSession(25);

            Assert.True(svc.IsInFocusMode);
            Assert.NotNull(svc.CurrentSession);
            Assert.True(started);

            // Cleanup
            svc.EndSession(false);
        }

        [Fact]
        public void EndSession_ExitsFocusMode()
        {
            var svc = new FocusSessionService();
            svc.StartSession(25);

            bool ended = false;
            bool completedFlag = false;
            svc.SessionEnded += (completed) => { ended = true; completedFlag = completed; };

            svc.EndSession(false);

            Assert.False(svc.IsInFocusMode);
            Assert.True(ended);
            Assert.False(completedFlag);
        }

        [Fact]
        public void Progress_IsZeroAtStart()
        {
            var svc = new FocusSessionService();
            svc.StartSession(25);

            // Progress should be near 0 at the start
            Assert.True(svc.Progress < 5, $"Progress should be near 0 at start, got {svc.Progress}");

            svc.EndSession(false);
        }

        [Fact]
        public void SetAppCategory_PersistsCategory()
        {
            var svc = new FocusSessionService();
            svc.SetAppCategory("test_app_123", "Test App", "C:\\test.exe", AppCategoryType.Entertainment);

            var category = svc.GetAppCategory("test_app_123");
            Assert.Equal(AppCategoryType.Entertainment, category);

            // Cleanup - set back to Uncategorized
            svc.SetAppCategory("test_app_123", "Test App", "C:\\test.exe", AppCategoryType.Uncategorized);
        }

        [Fact]
        public void IsDistractingApp_DetectsEntertainment()
        {
            var svc = new FocusSessionService();
            svc.BlockEntertainment = true;
            svc.SetAppCategory("distracting_test", "Game", "C:\\game.exe", AppCategoryType.Entertainment);

            Assert.True(svc.IsDistractingApp("distracting_test"));

            // Cleanup
            svc.SetAppCategory("distracting_test", "Game", "C:\\game.exe", AppCategoryType.Uncategorized);
        }

        [Fact]
        public void AllowAppForSession_OverridesDistraction()
        {
            var svc = new FocusSessionService();
            svc.BlockEntertainment = true;
            svc.SetAppCategory("override_test", "YouTube", "C:\\yt.exe", AppCategoryType.Entertainment);
            svc.StartSession(25);

            svc.AllowAppForSession("override_test");
            Assert.True(svc.IsAppOverriddenForSession("override_test"));

            svc.EndSession(false);

            // Cleanup
            svc.SetAppCategory("override_test", "YouTube", "C:\\yt.exe", AppCategoryType.Uncategorized);
        }

        // --- Windows Focus integration reaction ---

        [Fact]
        public void OnWindowsFocusActiveChanged_True_StartsASession()
        {
            var svc = new FocusSessionService();

            svc.OnWindowsFocusActiveChanged(true);

            Assert.True(svc.IsInFocusMode);
            svc.EndSession(false);
        }

        [Fact]
        public void OnWindowsFocusActiveChanged_False_DoesNothing()
        {
            var svc = new FocusSessionService();

            svc.OnWindowsFocusActiveChanged(false);

            Assert.False(svc.IsInFocusMode);
        }

        [Fact]
        public void OnWindowsFocusActiveChanged_True_DoesNotClobberAnInProgressSession()
        {
            var svc = new FocusSessionService();
            svc.StartSession(25);
            var originalStart = svc.CurrentSession!.StartTime;

            svc.OnWindowsFocusActiveChanged(true);

            Assert.True(svc.IsInFocusMode);
            Assert.Equal(originalStart, svc.CurrentSession!.StartTime);
            svc.EndSession(false);
        }

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

        [Fact]
        public void EverySession_IsKept()
        {
            DatabaseService.DeleteAllData();

            using var svc = new FocusSessionService();
            svc.StartSession(25);
            svc.EndSession(true);
            svc.StartSession(15);
            svc.EndSession(false);
            svc.StartSession(45);
            svc.EndSession(true);

            var sessions = DatabaseService.GetFocusSessionsForDate(DateTime.Today);
            Assert.Equal(new[] { 25, 15, 45 }, sessions.OrderBy(s => s.StartTime).Select(s => s.PlannedDurationMinutes));
            Assert.Equal(3, sessions.Select(s => s.Id).Distinct().Count());
        }
    }
}
