using System;
using digital_wellbeing_app.Helpers;
using Xunit;

namespace digital_wellbeing_app.Tests.Helpers
{
    public class TimeFormatHelperTests
    {
        [Theory]
        [InlineData(0, "0s")]
        [InlineData(20, "20s")]
        [InlineData(59.6, "60s")]
        public void FormatFocusMetric_SubMinuteDuration_RetainsSeconds(double seconds, string expected)
        {
            Assert.Equal(expected, TimeFormatHelper.FormatFocusMetric(TimeSpan.FromSeconds(seconds)));
        }

        [Fact]
        public void FormatFocusMetric_AtLeastOneMinute_UsesCompactFormat()
        {
            Assert.Equal("1m", TimeFormatHelper.FormatFocusMetric(TimeSpan.FromSeconds(60)));
        }
    }
}
