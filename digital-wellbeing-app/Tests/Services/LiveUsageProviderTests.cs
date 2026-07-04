using System;
using System.Linq;
using Xunit;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>
    /// Tests for the shared "today so far" provider (v2.2 Phase 1.2). Guarantees every view reads
    /// today's totals through one code path that folds the live in-memory session into persisted
    /// rows, so the Dashboard, App Usage page and reports can no longer disagree.
    /// </summary>
    public class LiveUsageProviderTests
    {
        [Fact]
        public void CombineTodayAppEntries_FoldsLiveSessionIntoMatchingApp()
        {
            DatabaseService.DeleteAllData();
            var now = DateTime.Now;
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "chrome",
                ExecutablePath = @"C:\chrome.exe",
                StartTime = now.AddMinutes(-30),
                EndTime = now.AddMinutes(-20) // 10 persisted minutes
            });

            var live = new AppUsageSession
            {
                AppName = "chrome",
                ExecutablePath = @"C:\chrome.exe",
                StartTime = now.AddMinutes(-5) // ~5 live minutes, no EndTime
            };

            var chrome = LiveUsageProvider.CombineTodayAppEntries(live)
                .Single(e => e.ExecutablePath == @"C:\chrome.exe");

            Assert.InRange(chrome.Duration.TotalMinutes, 14.5, 15.5);
        }

        [Fact]
        public void CombineTodayAppEntries_AddsLiveApp_NotYetPersisted()
        {
            DatabaseService.DeleteAllData();
            var live = new AppUsageSession
            {
                AppName = "code",
                ExecutablePath = @"C:\code.exe",
                StartTime = DateTime.Now.AddMinutes(-3)
            };

            var entries = LiveUsageProvider.CombineTodayAppEntries(live);

            Assert.Contains(entries, e => e.ExecutablePath == @"C:\code.exe"
                                          && e.Duration.TotalMinutes >= 2.5);
        }

        [Fact]
        public void CombineTodayAppEntries_PersistedOnly_WhenNoLiveSession()
        {
            DatabaseService.DeleteAllData();
            var now = DateTime.Now;
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "notepad",
                ExecutablePath = @"C:\notepad.exe",
                StartTime = now.AddMinutes(-15),
                EndTime = now.AddMinutes(-10)
            });

            var entry = LiveUsageProvider.CombineTodayAppEntries(null).Single();
            Assert.Equal(5, entry.Duration.TotalMinutes, 1);
        }

        [Fact]
        public void CombineTodayActiveTime_PrefersLive_FallsBackToPersisted()
        {
            Assert.Equal(TimeSpan.FromMinutes(42),
                LiveUsageProvider.CombineTodayActiveTime(TimeSpan.FromMinutes(42)));

            var persisted = LiveUsageProvider.CombineTodayActiveTime(null);
            Assert.True(persisted >= TimeSpan.Zero);
        }
    }
}
