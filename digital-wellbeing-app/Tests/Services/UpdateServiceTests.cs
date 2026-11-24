using digital_wellbeing_app.Services;
using Xunit;

namespace digital_wellbeing_app.Tests.Services
{
    public class UpdateServiceTests
    {
        [Fact]
        public void Constructor_InDevelopmentBuild_DoesNotRequireVelopackLocator()
        {
            var service = new UpdateService();
            Assert.True(service.IsAvailable || !string.IsNullOrWhiteSpace(service.UnavailableReason));
        }
    }
}
