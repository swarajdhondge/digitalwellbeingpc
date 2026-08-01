using System.Globalization;
using System.Windows.Media;
using digital_wellbeing_app.Converters;
using Xunit;

namespace digital_wellbeing_app.Tests.Helpers
{
    public class TrackingStateConvertersTests
    {
        [Fact]
        public void GoalProgressColor_InvalidBindingValue_ReturnsFallbackBrush()
        {
            var converter = new GoalProgressToColorConverter();

            var result = converter.Convert(null!, typeof(Brush), null!, CultureInfo.InvariantCulture);

            Assert.IsAssignableFrom<Brush>(result);
        }
    }
}
