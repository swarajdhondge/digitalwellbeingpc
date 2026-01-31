using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Tests.Services
{
    public class DatabaseServiceExtendedTests
    {
        [Fact]
        public void GetConnection_ReturnsSameInstance()
        {
            var conn1 = DatabaseService.GetConnection();
            var conn2 = DatabaseService.GetConnection();
            Assert.Same(conn1, conn2);
        }

        [Fact]
        public void GetScreenTimePeriodsForRange_ReturnsFilteredResults()
        {
            var farFuture = new DateTime(9998, 1, 1);

            var periods = DatabaseService.GetScreenTimePeriodsForRange(farFuture, farFuture);
            Assert.NotNull(periods);
            // Far future dates should have no records
            Assert.Empty(periods);
        }

        [Fact]
        public void GetAllScreenTimePeriods_ReturnsNonNullList()
        {
            var all = DatabaseService.GetAllScreenTimePeriods();
            Assert.NotNull(all);
        }

        [Fact]
        public void GetAllAppUsageSessions_ReturnsNonNullList()
        {
            var all = DatabaseService.GetAllAppUsageSessions();
            Assert.NotNull(all);
        }

        [Fact]
        public void GetAllSoundUsageSessions_ReturnsNonNullList()
        {
            var all = DatabaseService.GetAllSoundUsageSessions();
            Assert.NotNull(all);
        }

        [Fact]
        public void GetAllFocusSessions_ReturnsNonNullList()
        {
            var all = DatabaseService.GetAllFocusSessions();
            Assert.NotNull(all);
        }

        [Fact]
        public void GetDatabaseFilePath_ReturnsNonEmptyPath()
        {
            var path = DatabaseService.GetDatabaseFilePath();
            Assert.False(string.IsNullOrWhiteSpace(path));
            Assert.Contains("Digital Wellbeing", path);
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
        public void ConcurrentReads_DoNotThrow()
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

            Task.WaitAll(tasks.ToArray());
            Assert.Empty(exceptions);
        }

        [Fact]
        public void InsertAndRetrieveScreenTimePeriod_ThreadSafe()
        {
            var testDate = "9999-12-30";
            var conn = DatabaseService.GetConnection();

            // Clean up any existing test data
            var existing = conn.Table<ScreenTimePeriod>()
                .Where(x => x.SessionDate == testDate)
                .ToList();
            foreach (var item in existing)
                conn.Delete(item);

            // Insert
            var period = new ScreenTimePeriod
            {
                SessionDate = testDate,
                AccumulatedActiveSeconds = 500
            };
            conn.Insert(period);

            // Retrieve
            var saved = conn.Table<ScreenTimePeriod>()
                .FirstOrDefault(x => x.SessionDate == testDate);
            Assert.NotNull(saved);
            Assert.Equal(500, saved.AccumulatedActiveSeconds);

            // Cleanup
            conn.Delete(saved);
        }
    }
}
