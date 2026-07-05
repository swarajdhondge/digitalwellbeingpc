using System;
using System.Linq;
using Xunit;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>
    /// v2.2 Phase 6.3 cross-section consistency: the readers that used to disagree now share one
    /// source of truth, so the Dashboard's App Time equals the App Usage page total, and the
    /// Report's "today" screen bucket equals the Dashboard's live screen time.
    /// </summary>
    public class CrossSectionConsistencyTests
    {
        [Fact]
        public void DashboardAppTime_EqualsAppUsagePageTotal()
        {
            DatabaseService.DeleteAllData();
            // Mid-day anchor, not DateTime.Now: a "now minus 3h" session crosses into the previous
            // day when this runs shortly after local midnight (CI is UTC), dropping it from the
            // day-bucketed GetToday* readers and flaking the gate.
            var now = DateTime.Today.AddHours(12);
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "code", ExecutablePath = @"C:\code.exe",
                StartTime = now.AddHours(-3), EndTime = now.AddHours(-2)
            });
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "chrome", ExecutablePath = @"C:\chrome.exe",
                StartTime = now.AddMinutes(-90), EndTime = now.AddMinutes(-45)
            });

            // The Dashboard's App Time and the App Usage page both read GetTodayApp* — assert the
            // total and the per-app breakdown they share are internally consistent.
            var total = LiveUsageProvider.GetTodayAppTime();
            var perAppSum = LiveUsageProvider.GetTodayAppEntries()
                .Aggregate(TimeSpan.Zero, (s, e) => s + e.Duration);

            Assert.Equal(perAppSum, total);
            Assert.Equal(TimeSpan.FromMinutes(105), total); // 60 + 45, exact
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
    }
}
