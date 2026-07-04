using System;
using System.Linq;
using Xunit;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>
    /// Tests for v2.2 Phase 1.7 retention and housekeeping: usage tables must not grow forever,
    /// and users must be able to delete a specific date range.
    /// </summary>
    public class RetentionTests
    {
        [Fact]
        public void PurgeDataOlderThan_RemovesOldRows_KeepsRecent()
        {
            DatabaseService.DeleteAllData();
            var now = DateTime.Now;

            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "old", ExecutablePath = @"C:\old.exe",
                StartTime = now.AddYears(-2), EndTime = now.AddYears(-2).AddMinutes(5)
            });
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "recent", ExecutablePath = @"C:\recent.exe",
                StartTime = now.AddDays(-1), EndTime = now.AddDays(-1).AddMinutes(5)
            });
            DatabaseService.SaveScreenTimePeriod(new ScreenTimePeriod
            {
                SessionDate = now.AddYears(-2).ToString("yyyy-MM-dd"), AccumulatedActiveSeconds = 100
            });
            DatabaseService.SaveScreenTimePeriod(new ScreenTimePeriod
            {
                SessionDate = now.AddDays(-1).ToString("yyyy-MM-dd"), AccumulatedActiveSeconds = 200
            });

            DatabaseService.PurgeDataOlderThan(now.AddMonths(-12));

            var apps = DatabaseService.GetAppUsageSessionsForRange(now.AddYears(-3), now);
            Assert.DoesNotContain(apps, a => a.ExecutablePath == @"C:\old.exe");
            Assert.Contains(apps, a => a.ExecutablePath == @"C:\recent.exe");

            var periods = DatabaseService.GetScreenTimePeriodsForRange(now.AddYears(-3), now);
            Assert.DoesNotContain(periods, p => p.SessionDate == now.AddYears(-2).ToString("yyyy-MM-dd"));
            Assert.Contains(periods, p => p.SessionDate == now.AddDays(-1).ToString("yyyy-MM-dd"));
        }

        [Fact]
        public void DeleteDataInRange_RemovesOnlyRowsInRange()
        {
            DatabaseService.DeleteAllData();
            var inRange = new DateTime(2025, 3, 10, 9, 0, 0);
            var outside = new DateTime(2025, 4, 1, 9, 0, 0);

            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "in", ExecutablePath = @"C:\in.exe",
                StartTime = inRange, EndTime = inRange.AddMinutes(10)
            });
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "out", ExecutablePath = @"C:\out.exe",
                StartTime = outside, EndTime = outside.AddMinutes(10)
            });

            DatabaseService.DeleteDataInRange(new DateTime(2025, 3, 1), new DateTime(2025, 3, 31));

            var all = DatabaseService.GetAppUsageSessionsForRange(new DateTime(2025, 1, 1), new DateTime(2025, 12, 31));
            Assert.DoesNotContain(all, x => x.ExecutablePath == @"C:\in.exe");
            Assert.Contains(all, x => x.ExecutablePath == @"C:\out.exe");
        }

        [Fact]
        public void RetentionMonths_DefaultsTo12_AndRoundTrips()
        {
            var settings = new SettingsService();
            Assert.Equal(12, new SettingsService().LoadRetentionMonths());

            settings.SaveRetentionMonths(6);
            Assert.Equal(6, new SettingsService().LoadRetentionMonths());

            settings.SaveRetentionMonths(12); // restore default
        }
    }
}
