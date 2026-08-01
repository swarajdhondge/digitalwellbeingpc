using System;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;
using Xunit;

namespace digital_wellbeing_app.Tests.Helpers
{
    public class AppUsageMetricsTests
    {
        [Fact]
        public void PeriodicChunksForSameApp_AreOneFocusStretch()
        {
            var start = new DateTime(2026, 7, 31, 9, 0, 0);
            var sessions = new[]
            {
                Session("code", start, start.AddMinutes(5)),
                Session("code", start.AddMinutes(5), start.AddMinutes(10)),
                Session("code", start.AddMinutes(10), start.AddMinutes(15))
            };

            var result = AppUsageMetrics.Calculate(sessions);

            Assert.Equal(0, result.SwitchCount);
            Assert.Equal(TimeSpan.FromMinutes(15), result.AverageFocusTime);
            Assert.Equal(TimeSpan.FromMinutes(15), result.LongestFocusTime);
        }

        [Fact]
        public void DifferentApps_CountOnlyRealTransitions()
        {
            var start = new DateTime(2026, 7, 31, 9, 0, 0);
            var sessions = new[]
            {
                Session("code", start, start.AddMinutes(5)),
                Session("code", start.AddMinutes(5), start.AddMinutes(10)),
                Session("chrome", start.AddMinutes(10), start.AddMinutes(14))
            };

            var result = AppUsageMetrics.Calculate(sessions);

            Assert.Equal(1, result.SwitchCount);
            Assert.Equal(TimeSpan.FromMinutes(7), result.AverageFocusTime);
            Assert.Equal(TimeSpan.FromMinutes(10), result.LongestFocusTime);
        }

        private static AppUsageSession Session(string app, DateTime start, DateTime end) => new()
        {
            AppName = app,
            ExecutablePath = $@"C:\\{app}.exe",
            StartTime = start,
            EndTime = end
        };
    }
}
