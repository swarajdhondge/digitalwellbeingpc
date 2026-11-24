using System;
using System.Linq;
using digital_wellbeing_app.Models;
using Xunit;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    public class ReportServiceTests : TestBase
    {
        [Fact]
        public void GetWeekStart_ReturnsMonday()
        {
            // January 31, 2026 is a Saturday
            var date = new DateTime(2026, 1, 31);
            var weekStart = ReportService.GetWeekStart(date);

            Assert.Equal(DayOfWeek.Monday, weekStart.DayOfWeek);
            Assert.True(weekStart <= date);
            Assert.True((date - weekStart).TotalDays < 7);
        }

        [Fact]
        public void GetWeekEnd_ReturnsSunday()
        {
            var date = new DateTime(2026, 1, 31);
            var weekEnd = ReportService.GetWeekEnd(date);

            Assert.Equal(DayOfWeek.Sunday, weekEnd.DayOfWeek);
            Assert.True(weekEnd >= date);
        }

        [Fact]
        public void GetWeekStart_MondayIsItself()
        {
            // Find a Monday
            var monday = new DateTime(2026, 1, 26); // Monday
            Assert.Equal(DayOfWeek.Monday, monday.DayOfWeek);

            var weekStart = ReportService.GetWeekStart(monday);
            Assert.Equal(monday.Date, weekStart.Date);
        }

        [Fact]
        public void GetDailyScreenTimeTrend_ReturnsCorrectDayCount()
        {
            var svc = new ReportService();
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-6); // 7 days

            var trend = svc.GetDailyScreenTimeTrend(startDate, endDate);
            Assert.NotNull(trend);
            Assert.Equal(7, trend.Count);
        }

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
        public void ReportTodayScreenBucket_EqualsLiveTodayActiveTime()
        {
            DatabaseService.DeleteAllData();
            var today = DateTime.Today;
            DatabaseService.SaveScreenTimePeriod(new ScreenTimePeriod
            {
                SessionDate = today.ToString("yyyy-MM-dd"),
                AccumulatedActiveSeconds = 7200 // 2h persisted
            });

            var live = LiveUsageProvider.GetTodayActiveTime();
            var trend = new ReportService().GetDailyScreenTimeTrend(today, today);
            var todayBucket = trend.Single(d => d.Date.Date == today);

            // ReportService's today bucket is wired to the same live source as the Dashboard.
            Assert.Equal((int)live.TotalSeconds, todayBucket.TotalSeconds);
        }

        [Fact]
        public void CurrentWeekComparison_EqualsWeeklyTrendTotal()
        {
            DatabaseService.DeleteAllData();
            var today = DateTime.Today;
            DatabaseService.SaveScreenTimePeriod(new ScreenTimePeriod
            {
                SessionDate = today.ToString("yyyy-MM-dd"),
                SessionStartTime = today.ToString("o"),
                LastRecordedTime = today.ToString("o"),
                AccumulatedActiveSeconds = 5432
            });

            var reports = new ReportService();
            var weekStart = ReportService.GetWeekStart(today);
            var trendTotal = reports.GetDailyScreenTimeTrend(weekStart, weekStart.AddDays(6))
                .Sum(d => d.TotalSeconds);
            var comparison = reports.GetWeekOverWeekComparison(weekStart);

            Assert.Equal(trendTotal, (int)comparison.ThisWeekScreenTime.TotalSeconds);
        }
    }
}
