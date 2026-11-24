using Xunit;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    public class NotificationRoutingTests
    {
        [Theory]
        [InlineData(NotificationRouting.Goal, NotificationTarget.Dashboard)]
        [InlineData(NotificationRouting.Limit, NotificationTarget.Limits)]
        [InlineData(NotificationRouting.WindDown, NotificationTarget.Settings)]
        [InlineData(NotificationRouting.Hearing, NotificationTarget.Sound)]
        [InlineData(NotificationRouting.Hint, NotificationTarget.ShowOnly)]
        [InlineData("something-else", NotificationTarget.ShowOnly)]
        [InlineData(null, NotificationTarget.ShowOnly)]
        public void Resolve_MapsAlertToPage(string? action, NotificationTarget expected)
            => Assert.Equal(expected, NotificationRouting.Resolve(action, false, false));

        [Fact]
        public void Break_OpensOverlayOnlyWhilePending()
        {
            Assert.Equal(NotificationTarget.BreakOverlay, NotificationRouting.Resolve(NotificationRouting.Break, true, false));
            Assert.Equal(NotificationTarget.ShowOnly, NotificationRouting.Resolve(NotificationRouting.Break, false, false));
        }

        [Fact]
        public void Focus_OpensOverlayOnlyWhileADistractionIsActive()
        {
            Assert.Equal(NotificationTarget.FocusOverlay, NotificationRouting.Resolve(NotificationRouting.Focus, false, true));
            Assert.Equal(NotificationTarget.ShowOnly, NotificationRouting.Resolve(NotificationRouting.Focus, false, false));
        }
    }
}
