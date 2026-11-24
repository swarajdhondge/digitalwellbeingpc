using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Tests.Services
{
    public class DatabaseServiceTests : TestBase
    {
        [Fact]
        public void CanInsertAndRetrieveSession()
        {
            // Arrange
            var conn = DatabaseService.GetConnection();
            var testDate = "9999-12-31"; // hopefully doesn't collide with real data

            // Clean up any existing
            conn.Table<ScreenTimePeriod>()
                .Where(x => x.SessionDate == testDate)
                .ToList()
                .ForEach(item => conn.Delete(item));

            // Act
            var newSession = new ScreenTimePeriod
            {
                SessionDate = testDate,
                SessionStartTime = "01:23 PM",
                LastRecordedTime = "01:45 PM",
                AccumulatedActiveSeconds = 999
            };

            conn.Insert(newSession);

            // Assert
            var saved = conn.Table<ScreenTimePeriod>()
                            .FirstOrDefault(x => x.SessionDate == testDate);
            Assert.NotNull(saved);
            Assert.Equal(999, saved.AccumulatedActiveSeconds);

            // Cleanup
            conn.Delete(saved);
        }

        [Fact]
        public void GetDatabaseFileSize_ReturnsPositiveValue()
        {
            // Ensure DB exists by getting connection
            DatabaseService.GetConnection();
            var size = DatabaseService.GetDatabaseFileSize();
            Assert.True(size > 0, $"DB file size should be positive, got {size}");
        }

        [Fact]
        public async Task ConcurrentReads_DoNotThrow()
        {
            // Verify thread safety by reading from multiple threads simultaneously
            var tasks = new List<Task>();
            var exceptions = new List<Exception>();

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        var conn = DatabaseService.GetConnection();
                        Assert.NotNull(conn);
                        var periods = DatabaseService.GetAllScreenTimePeriods();
                        Assert.NotNull(periods);
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);
            Assert.Empty(exceptions);
        }

        [Fact]
        public void SaveAppUsageSession_RejectsReversedInterval()
        {
            DatabaseService.DeleteAllData();
            var now = DateTime.Today.AddHours(12);
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
            var now = DateTime.Today.AddHours(12);
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
            var now = DateTime.Today.AddHours(12);
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
            var now = DateTime.Today.AddHours(12);
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
            var now = DateTime.Today.AddHours(12);
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
