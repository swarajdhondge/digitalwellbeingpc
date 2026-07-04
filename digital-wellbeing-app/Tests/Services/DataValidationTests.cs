using System;
using System.Linq;
using Xunit;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>
    /// Tests for write-side validation and read-side filtering (v2.2 Phase 1.3). A single row with
    /// EndTime before StartTime, or an absurd duration from a clock change, used to silently
    /// corrupt every total that sums (End - Start).
    /// </summary>
    public class DataValidationTests
    {
        [Fact]
        public void SaveAppUsageSession_RejectsReversedInterval()
        {
            DatabaseService.DeleteAllData();
            var now = DateTime.Now;
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "x",
                ExecutablePath = @"C:\x.exe",
                StartTime = now,
                EndTime = now.AddMinutes(-5) // reversed
            });

            Assert.Empty(DatabaseService.GetAppUsageSessionsForDate(now));
        }

        [Fact]
        public void SaveAppUsageSession_RejectsDurationOver24h()
        {
            DatabaseService.DeleteAllData();
            var now = DateTime.Now;
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "x",
                ExecutablePath = @"C:\x.exe",
                StartTime = now.AddHours(-25),
                EndTime = now
            });

            Assert.Empty(DatabaseService.GetAppUsageSessionsForRange(now.AddDays(-2), now));
        }

        [Fact]
        public void SaveAppUsageSession_AcceptsValidInterval()
        {
            DatabaseService.DeleteAllData();
            var now = DateTime.Now;
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "x",
                ExecutablePath = @"C:\x.exe",
                StartTime = now.AddMinutes(-40),
                EndTime = now.AddMinutes(-38)
            });

            Assert.Single(DatabaseService.GetAppUsageSessionsForDate(now));
        }

        [Fact]
        public void GetAppUsageSessionsForRange_FiltersPreexistingCorruptRows()
        {
            DatabaseService.DeleteAllData();
            var now = DateTime.Now;
            // Bypass validation to simulate a corrupt row written by an older build.
            DatabaseService.GetConnection().Insert(new AppUsageSession
            {
                AppName = "bad",
                ExecutablePath = @"C:\bad.exe",
                StartTime = now,
                EndTime = now.AddMinutes(-10)
            });

            var range = DatabaseService.GetAppUsageSessionsForRange(now.Date, now.Date);
            Assert.DoesNotContain(range, s => s.ExecutablePath == @"C:\bad.exe");
        }

        [Fact]
        public void SaveSoundSession_RejectsReversedInterval()
        {
            DatabaseService.DeleteAllData();
            var now = DateTime.Now;
            DatabaseService.SaveSoundSession(new SoundUsageSession
            {
                StartTime = now,
                EndTime = now.AddMinutes(-1)
            });

            Assert.Empty(DatabaseService.GetSoundSessionsForDate(now));
        }

        [Fact]
        public void SaveScreenTimeSession_RejectsNegativeOrHugeDuration()
        {
            DatabaseService.DeleteAllData();
            var key = DateTime.Today.ToString("yyyy-MM-dd");
            DatabaseService.SaveScreenTimeSession(new ScreenTimeSession
            {
                SessionDate = key, StartTime = DateTime.Now, DurationSeconds = -5
            });
            DatabaseService.SaveScreenTimeSession(new ScreenTimeSession
            {
                SessionDate = key, StartTime = DateTime.Now, DurationSeconds = 90000 // > 24h
            });

            Assert.Empty(DatabaseService.GetScreenTimeSessionsForDate(DateTime.Today));
        }
    }
}
