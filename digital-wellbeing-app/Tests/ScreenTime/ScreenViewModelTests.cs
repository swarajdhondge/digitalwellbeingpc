using Xunit;
using digital_wellbeing_app.ViewModels;

namespace digital_wellbeing_app.Tests.ScreenTime
{
    public class ScreenViewModelTests
    {
        [Fact]
        public void LoadWeeklyUsage_ShouldPopulateSevenDays()
        {
            // Arrange
            var vm = new ScreenViewModel();

            // Act
            vm.LoadWeeklyUsage();

            // Assert
            // We expect 7 items (Mon-Sun) in the collection
            Assert.Equal(7, vm.WeeklyUsage.Count);
        }
    }
}
