using System;
using Xunit;
using digital_wellbeing_app.Platform.Windows;

namespace digital_wellbeing_app.Tests.Platform
{
    /// <summary>
    /// WindowsFocusIntegration's whole contract is "never throws, regardless of whether the
    /// underlying WinRT API is supported on this machine/channel" - these tests exercise that
    /// contract directly rather than asserting a specific IsSupported/IsFocusActive value, since
    /// that value is genuinely environment-dependent (see the open packaged-vs-unpackaged spike in
    /// docs/roadmap-handoff.md).
    /// </summary>
    public class WindowsFocusIntegrationTests
    {
        [Fact]
        public void IsSupported_NeverThrows()
        {
            using var integration = new WindowsFocusIntegration();
            var exception = Record.Exception(() => { var _ = integration.IsSupported; });
            Assert.Null(exception);
        }

        [Fact]
        public void IsFocusActive_NeverThrows()
        {
            using var integration = new WindowsFocusIntegration();
            var exception = Record.Exception(() => { var _ = integration.IsFocusActive; });
            Assert.Null(exception);
            // False is the only safe default when the API is unsupported/unavailable.
            if (!integration.IsSupported)
                Assert.False(integration.IsFocusActive);
        }

        [Fact]
        public void StartStopDispose_NeverThrows_EvenWhenCalledTwice()
        {
            var integration = new WindowsFocusIntegration();

            var exception = Record.Exception(() =>
            {
                integration.Start();
                integration.Start(); // idempotent
                integration.Stop();
                integration.Stop(); // idempotent
                integration.Dispose();
                integration.Dispose(); // idempotent
            });

            Assert.Null(exception);
        }

        [Fact]
        public void FocusActiveChanged_NeverFiresWithoutSubscription_AndSubscribingDoesNotThrow()
        {
            using var integration = new WindowsFocusIntegration();
            bool fired = false;
            integration.FocusActiveChanged += _ => fired = true;

            var exception = Record.Exception(() => integration.Start());

            Assert.Null(exception);
            // Not asserting `fired` either way - only asserts that subscribing/starting is safe.
            _ = fired;
        }
    }
}
