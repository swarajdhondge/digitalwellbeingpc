using Xunit;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    public class TestNotifyRequestTests
    {
        [Theory]
        [InlineData("goal")]
        [InlineData("limit")]
        [InlineData("break")]
        [InlineData("winddown")]
        [InlineData("focus")]
        [InlineData("hearing")]
        [InlineData("hint")]
        public void EveryAlert_IsAcceptedInTestMode(string alert)
            => Assert.Equal(alert, TestNotifyRequest.Parse(new[] { "--test-notify", alert }, "1"));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("0")]
        [InlineData("true")]
        public void WithoutTestMode_TheSwitchDoesNothing(string? mode)
            => Assert.Null(TestNotifyRequest.Parse(new[] { "--test-notify", "goal" }, mode));

        [Fact]
        public void UnknownAlert_MissingValue_OrNoSwitch_GivesNothing()
        {
            Assert.Null(TestNotifyRequest.Parse(new[] { "--test-notify", "nope" }, "1"));
            Assert.Null(TestNotifyRequest.Parse(new[] { "--test-notify" }, "1"));
            Assert.Null(TestNotifyRequest.Parse(new[] { "--startup" }, "1"));
        }

        [Fact]
        public void SwitchIsFoundAmongOtherArguments_IgnoringCase()
            => Assert.Equal("break", TestNotifyRequest.Parse(new[] { "--startup", "--TEST-NOTIFY", "BREAK" }, "1"));
    }
}
