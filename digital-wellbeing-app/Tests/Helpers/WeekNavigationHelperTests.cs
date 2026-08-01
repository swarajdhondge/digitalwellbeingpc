using System;
using digital_wellbeing_app.Helpers;
using Xunit;

namespace digital_wellbeing_app.Tests.Helpers
{
    public class WeekNavigationHelperTests
    {
        [Theory]
        [InlineData(2026, 7, 27, 27)]
        [InlineData(2026, 7, 31, 27)]
        [InlineData(2026, 8, 2, 27)]
        [InlineData(2026, 8, 3, 3)]
        public void StartOfWeek_UsesMonday(int year, int month, int day, int expectedDay)
        {
            var start = WeekNavigationHelper.StartOfWeek(new DateTime(year, month, day));
            Assert.Equal(DayOfWeek.Monday, start.DayOfWeek);
            Assert.Equal(expectedDay, start.Day);
        }

        [Fact]
        public void Clamp_DoesNotNavigateBeforeStoredHistory()
        {
            var earliest = new DateTime(2026, 6, 15);
            var selected = WeekNavigationHelper.Clamp(new DateTime(2025, 1, 1), earliest);
            Assert.Equal(earliest, selected);
        }

        [Fact]
        public void FormatWeek_IncludesWeekNumberAndRange()
        {
            var label = WeekNavigationHelper.FormatWeek(new DateTime(2026, 7, 27));
            Assert.Contains("W31", label);
            Assert.Contains("Jul 27", label);
            Assert.Contains("Aug 2", label);
        }
    }
}
