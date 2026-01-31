using System;
using Xunit;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    public class ReportServiceTests
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
        public void GetWeeklyReport_ReturnsValidData()
        {
            var svc = new ReportService();
            var weekStart = ReportService.GetWeekStart(DateTime.Today);
            var report = svc.GetWeeklyReport(weekStart);

            Assert.NotNull(report);
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

        [Fact]
        public void GetTopAppsForPeriod_RespectsTopCount()
        {
            var svc = new ReportService();
            var endDate = DateTime.Today;
            var startDate = endDate.AddDays(-6);

            var apps = svc.GetTopAppsForPeriod(startDate, endDate, topCount: 3);
            Assert.NotNull(apps);
            Assert.True(apps.Count <= 3);
        }

        [Fact]
        public void GetWeekOverWeekComparison_ReturnsData()
        {
            var svc = new ReportService();
            var weekStart = ReportService.GetWeekStart(DateTime.Today);
            var comparison = svc.GetWeekOverWeekComparison(weekStart);
            Assert.NotNull(comparison);
        }

        [Fact]
        public void GetCurrentWeekSummary_ReturnsTuple()
        {
            var svc = new ReportService();
            var (totalTime, changePercent, improved) = svc.GetCurrentWeekSummary();

            Assert.True(totalTime.TotalSeconds >= 0);
            // changePercent and improved are calculated values, just verify they don't throw
        }
    }
}
