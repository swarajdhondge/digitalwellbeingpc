using System.Reflection;
using Xunit;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests.Services
{
    /// <summary>
    /// Covers 2026-07-17's widened device-type keyword match and the new manual override.
    /// IdentifyDeviceType is private static (SoundMonitoringService itself needs a real audio
    /// device to construct, which isn't available in CI) - invoked via reflection so these tests
    /// exercise the real matching logic, not a reimplementation of it.
    /// </summary>
    public class SoundMonitoringServiceDeviceTypeTests : TestBase
    {
        private static string IdentifyDeviceType(string friendlyName)
        {
            var method = typeof(SoundMonitoringService).GetMethod("IdentifyDeviceType", BindingFlags.NonPublic | BindingFlags.Static)!;
            return (string)method.Invoke(null, new object[] { friendlyName })!;
        }

        [Theory]
        [InlineData("Realtek Headphones", "Headphones")]
        [InlineData("Beats Studio3", "Headphones")]
        [InlineData("Sony WH-1000XM5", "Headphones")]
        [InlineData("AirPods Pro", "Earphones")]
        [InlineData("Samsung Galaxy Buds2", "Earphones")]
        [InlineData("Generic USB Earbuds", "Earphones")]
        [InlineData("Sony WF-1000XM4", "Earphones")]
        [InlineData("Jabra Evolve Headset", "Headsets")]
        [InlineData("Logitech Z407 Speakers", "Speakers")]
        [InlineData("Yamaha YAS Soundbar", "Speakers")]
        [InlineData("Some Unrecognized Device XYZ", "Unknown")]
        public void IdentifyDeviceType_WidenedKeywords_MatchesExpectedCategory(string friendlyName, string expected)
        {
            // Ensure no leftover override from a previous test in this run affects the guess.
            new SettingsService().SaveDeviceTypeOverride(null);

            Assert.Equal(expected, IdentifyDeviceType(friendlyName));
        }

        [Fact]
        public void IdentifyDeviceType_ManualOverride_WinsOverFriendlyNameGuess()
        {
            var settings = new SettingsService();
            settings.SaveDeviceTypeOverride("Speakers");

            // "Headphones" is right there in the name, but the override must win.
            Assert.Equal("Speakers", IdentifyDeviceType("Totally Generic Headphones"));

            settings.SaveDeviceTypeOverride(null);
        }

        [Fact]
        public void IdentifyDeviceType_NoOverride_FallsBackToGuess()
        {
            new SettingsService().SaveDeviceTypeOverride(null);
            Assert.Equal("Headphones", IdentifyDeviceType("Some Headphones Model"));
        }
    }
}
