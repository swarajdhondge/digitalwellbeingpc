using System;
using System.Reflection;
using Xunit;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    public class WebsiteUsageServiceTests : TestBase
    {
        [Fact]
        public void SaveWebsiteUsageSession_ThenGetForDate_RoundTrips()
        {
            var today = DateTime.Today;
            DatabaseService.SaveWebsiteUsageSession(new WebsiteUsageSession
            {
                BrowserProcessName = "chrome",
                Hostname = "youtube.com",
                StartTime = today.AddHours(10),
                EndTime = today.AddHours(10).AddMinutes(5)
            });

            var sessions = DatabaseService.GetWebsiteUsageSessionsForDate(today);
            Assert.Single(sessions);
            Assert.Equal("youtube.com", sessions[0].Hostname);
            Assert.Equal("chrome", sessions[0].BrowserProcessName);
        }

        [Fact]
        public void SaveWebsiteUsageSession_InvertedInterval_IsRejected()
        {
            var today = DateTime.Today;
            DatabaseService.SaveWebsiteUsageSession(new WebsiteUsageSession
            {
                BrowserProcessName = "chrome",
                Hostname = "bad.example",
                StartTime = today.AddHours(10),
                EndTime = today.AddHours(9) // ends before it starts
            });

            Assert.Empty(DatabaseService.GetWebsiteUsageSessionsForDate(today));
        }

        [Fact]
        public void GetWebsiteUsageSessionsForDate_OnlyReturnsMatchingDay()
        {
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            DatabaseService.SaveWebsiteUsageSession(new WebsiteUsageSession
            {
                BrowserProcessName = "msedge",
                Hostname = "today.example",
                StartTime = today.AddHours(1),
                EndTime = today.AddHours(1).AddMinutes(1)
            });
            DatabaseService.SaveWebsiteUsageSession(new WebsiteUsageSession
            {
                BrowserProcessName = "msedge",
                Hostname = "yesterday.example",
                StartTime = yesterday.AddHours(1),
                EndTime = yesterday.AddHours(1).AddMinutes(1)
            });

            var todaySessions = DatabaseService.GetWebsiteUsageSessionsForDate(today);
            Assert.Single(todaySessions);
            Assert.Equal("today.example", todaySessions[0].Hostname);
        }

        [Fact]
        public void DeleteAllData_RemovesWebsiteUsageSessions()
        {
            DatabaseService.SaveWebsiteUsageSession(new WebsiteUsageSession
            {
                BrowserProcessName = "chrome",
                Hostname = "erase.example",
                StartTime = DateTime.Today.AddHours(1),
                EndTime = DateTime.Today.AddHours(1).AddMinutes(1)
            });

            DatabaseService.DeleteAllData();

            Assert.Empty(DatabaseService.GetWebsiteUsageSessionsForDate(DateTime.Today));
        }

        [Fact]
        public void MidnightBoundary_SplitsWebsiteUsageIntoCorrectDays()
        {
            var day = new DateTime(2026, 7, 30);
            using var svc = new WebsiteUsageService();
            var current = typeof(WebsiteUsageService).GetField(
                "_currentSession", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var split = typeof(WebsiteUsageService).GetMethod(
                "SplitAtDayBoundaryLocked", BindingFlags.NonPublic | BindingFlags.Instance)!;

            current.SetValue(svc, new WebsiteUsageSession
            {
                BrowserProcessName = "chrome",
                Hostname = "example.com",
                StartTime = day.AddHours(23).AddMinutes(59),
                EndTime = day.AddDays(1).AddMinutes(1)
            });
            split.Invoke(svc, new object[] { day.AddDays(1).AddMinutes(1) });

            var prior = DatabaseService.GetWebsiteUsageSessionsForDate(day);
            Assert.Single(prior);
            Assert.Equal(day.AddDays(1), prior[0].EndTime);

            var live = Assert.IsType<WebsiteUsageSession>(current.GetValue(svc));
            Assert.Equal(day.AddDays(1), live.StartTime);
            Assert.Equal(day.AddDays(1).AddMinutes(1), live.EndTime);
        }

        // --- Service lifecycle smoke tests (no live browser required) ---

        [Fact]
        public void StartStopSetEnabled_NeverThrows()
        {
            using var svc = new WebsiteUsageService();

            var exception = Record.Exception(() =>
            {
                svc.Start();
                svc.Start(); // idempotent
                Assert.True(svc.IsRunning);
                svc.Stop();
                svc.Stop(); // idempotent
                Assert.False(svc.IsRunning);
                svc.SetEnabled(true);
                Assert.True(svc.IsRunning);
                svc.SetEnabled(false);
                Assert.False(svc.IsRunning);
            });

            Assert.Null(exception);
        }

        [Fact]
        public void Dispose_StopsAndNeverThrowsEvenWhenCalledTwice()
        {
            var svc = new WebsiteUsageService();
            svc.Start();

            var exception = Record.Exception(() =>
            {
                svc.Dispose();
                svc.Dispose(); // idempotent
            });

            Assert.Null(exception);
        }
    }
}
