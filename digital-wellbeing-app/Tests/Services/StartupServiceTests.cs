using digital_wellbeing_app.Services;
using Xunit;

namespace digital_wellbeing_app.Tests.Services
{
    public class StartupServiceTests
    {
        [Fact]
        public void StartupArgument_IsDetected()
        {
            StartupService.DetectStartupLaunch(new[] { "--startup" });

            Assert.True(StartupService.IsStartupLaunch);
        }

        [Fact]
        public void InteractiveLaunch_IsNotClassifiedAsStartup()
        {
            StartupService.DetectStartupLaunch(System.Array.Empty<string>());

            Assert.False(StartupService.IsStartupLaunch);
        }
    }
}
