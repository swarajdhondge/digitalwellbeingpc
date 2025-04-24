using System;
using System.Linq;
using Xunit;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Models;

namespace digital_wellbeing_app.Tests.ScreenTime
{
    public class DatabaseServiceTests
    {
        [Fact]
        public void GetConnection_Should_NotBeNull()
        {
            // Act
            var conn = DatabaseService.GetConnection();

            // Assert
            Assert.NotNull(conn);
        }

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
    }
}
