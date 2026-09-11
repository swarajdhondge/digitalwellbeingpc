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
    public class LiveUsageProviderTests : TestBase
    {
        [Fact]
        public void CombineTodayAppEntries_FoldsLiveSessionIntoMatchingApp()
        {
            DatabaseService.DeleteAllData();
            // Persisted row is anchored to mid-day today so it stays in today's bucket even when
            // this runs just after UTC midnight. The live session keeps real DateTime.Now because
            // the provider folds it as (DateTime.Now - StartTime).
            var midday = DateTime.Today.AddHours(9);
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "chrome",
                ExecutablePath = @"C:\chrome.exe",
                StartTime = midday,
                EndTime = midday.AddMinutes(10) // 10 persisted minutes
            });

            var live = new AppUsageSession
            {
                AppName = "chrome",
                ExecutablePath = @"C:\chrome.exe",
                StartTime = DateTime.Now.AddMinutes(-5) // ~5 live minutes, no EndTime
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
            // Anchor to mid-day today so the row stays in today's bucket near UTC midnight.
            var midday = DateTime.Today.AddHours(9);
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "notepad",
                ExecutablePath = @"C:\notepad.exe",
                StartTime = midday,
                EndTime = midday.AddMinutes(5)
            });

            var entry = LiveUsageProvider.CombineTodayAppEntries(null).Single();
            Assert.Equal(5, entry.Duration.TotalMinutes, 1);
        }

        [Fact]
        public void CombineTodayAppEntries_DoesNotCollapseDifferentAppsWithBlankPaths()
        {
            DatabaseService.DeleteAllData();
            var midday = DateTime.Today.AddHours(9);
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "Protected App A", ExecutablePath = string.Empty,
                StartTime = midday, EndTime = midday.AddMinutes(5)
            });
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "Protected App B", ExecutablePath = string.Empty,
                StartTime = midday.AddMinutes(5), EndTime = midday.AddMinutes(12)
            });

            var entries = LiveUsageProvider.CombineTodayAppEntries(null);

            Assert.Equal(2, entries.Count);
            Assert.Contains(entries, e => e.AppName == "Protected App A");
            Assert.Contains(entries, e => e.AppName == "Protected App B");
        }

        [Fact]
        public void GetTodayActiveTime_EqualsAppDurationSum()
        {
            // GetTodayActiveTime is now defined as the sum of per-app durations,
            // ensuring the headline always matches the per-app breakdown.
            DatabaseService.DeleteAllData();
            var midday = DateTime.Today.AddHours(9);
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "chrome",
                ExecutablePath = @"C:\chrome.exe",
                StartTime = midday,
                EndTime = midday.AddMinutes(10)
            });
            DatabaseService.SaveAppUsageSession(new AppUsageSession
            {
                AppName = "notepad",
                ExecutablePath = @"C:\notepad.exe",
                StartTime = midday.AddMinutes(10),
                EndTime = midday.AddMinutes(15)
            });

            var active = LiveUsageProvider.GetTodayActiveTime();
            var appSum = LiveUsageProvider.GetTodayAppTime();
            Assert.Equal(appSum.TotalSeconds, active.TotalSeconds, 1);
        }
    }
}
