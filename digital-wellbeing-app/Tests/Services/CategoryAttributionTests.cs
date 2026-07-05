using System;
using System.Linq;
using Xunit;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>
    /// Regression tests for the v2.2 Phase 1.1 category-attribution bug: category rows were keyed
    /// by process name (as Focus Mode writes them) but the reports looked them up by full
    /// executable path, so every app fell through to Uncategorized regardless of the user's choice.
    /// Sessions here deliberately carry the full path while categories carry the process name.
    /// </summary>
    public class CategoryAttributionTests
    {
        private static void SeedSession(string appName, string execPath, DateTime start, int minutes)
        {
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = appName,
                ExecutablePath = execPath,
                StartTime = start,
                EndTime = start.AddMinutes(minutes)
            });
        }

        private static void SeedCategory(string processName, string execPath, AppCategoryType category)
        {
            DatabaseService.SaveAppCategory(new AppCategory
            {
                AppIdentifier = processName,
                AppName = processName,
                ExecutablePath = execPath,
                Category = category,
                LastUpdated = new DateTime(2020, 1, 1)
            });
        }

        [Fact]
        public void GetFocusVsLeisureTime_AttributesByCategory_DespitePathVsProcessNameKeys()
        {
            DatabaseService.DeleteAllData(); // isolate usage rows from other tests
            var weekStart = new DateTime(2020, 6, 1);
            var weekEnd = weekStart.AddDays(6);

            SeedCategory("devenv", @"C:\VS\Common7\IDE\devenv.exe", AppCategoryType.Work);
            SeedCategory("vlc", @"C:\Program Files\VideoLAN\VLC\vlc.exe", AppCategoryType.Entertainment);

            SeedSession("devenv", @"C:\VS\Common7\IDE\devenv.exe", weekStart.AddHours(9), 60);  // 1h work
            SeedSession("vlc", @"C:\Program Files\VideoLAN\VLC\vlc.exe", weekStart.AddHours(20), 30); // 30m leisure

            var result = new ReportService().GetFocusVsLeisureTime(weekStart, weekEnd);

            // Before the fix both were 0 (everything counted as "other/uncategorized").
            Assert.Equal(60, result.FocusTime.TotalMinutes, 1);
            Assert.Equal(30, result.LeisureTime.TotalMinutes, 1);
            Assert.Equal(0, result.OtherTime.TotalMinutes, 1);
        }

        [Fact]
        public void GetTopAppsForPeriod_AssignsCategory_NotUncategorized()
        {
            DatabaseService.DeleteAllData();
            var weekStart = new DateTime(2020, 9, 7);
            var weekEnd = weekStart.AddDays(6);

            SeedCategory("photoshop", @"C:\Adobe\photoshop.exe", AppCategoryType.Work);
            SeedSession("photoshop", @"C:\Adobe\photoshop.exe", weekStart.AddDays(1).AddHours(10), 45);

            var apps = new ReportService().GetTopAppsForPeriod(weekStart, weekEnd, topCount: 10);

            var photoshop = apps.FirstOrDefault(a => a.ExecutablePath == @"C:\Adobe\photoshop.exe");
            Assert.NotNull(photoshop);
            Assert.Equal(AppCategoryType.Work, photoshop!.Category); // was Uncategorized before the fix
        }

        [Fact]
        public void NormalizeAppCategoryKeys_RewritesLegacyPathKeys_Idempotently()
        {
            DatabaseService.DeleteAllData();
            // A legacy row whose AppIdentifier was stored as a full path (the historical mistake).
            DatabaseService.SaveAppCategory(new AppCategory
            {
                AppIdentifier = @"C:\Legacy\slack.exe",
                AppName = "slack",
                ExecutablePath = @"C:\Legacy\slack.exe",
                Category = AppCategoryType.Work,
                LastUpdated = new DateTime(2020, 1, 1)
            });

            DatabaseService.NormalizeAppCategoryKeys();
            var afterFirst = DatabaseService.GetAppCategory("slack");
            Assert.NotNull(afterFirst);
            Assert.Equal("slack", afterFirst!.AppIdentifier);

            // Idempotent: a second pass changes nothing.
            DatabaseService.NormalizeAppCategoryKeys();
            var afterSecond = DatabaseService.GetAppCategory("slack");
            Assert.NotNull(afterSecond);
            Assert.Equal("slack", afterSecond!.AppIdentifier);
        }
    }
}
