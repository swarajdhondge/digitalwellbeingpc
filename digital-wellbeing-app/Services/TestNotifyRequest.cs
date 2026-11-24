using System;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Lets a test run show one alert straight away instead of waiting for the real trigger.
    /// It needs both the <c>--test-notify &lt;alert&gt;</c> switch and the environment variable
    /// <c>PULSE_TEST_MODE=1</c>. The variable is set by the test script that starts the app, never
    /// by Pulse or its installers, so a normal launch (Start menu, startup, shortcut) cannot use it.
    /// The alert is shown through the same path and with the same launch argument as a real one.
    /// </summary>
    public static class TestNotifyRequest
    {
        public const string SwitchName = "--test-notify";
        public const string ModeVariable = "PULSE_TEST_MODE";

        private static readonly string[] Alerts =
        {
            NotificationRouting.Goal, NotificationRouting.Limit, NotificationRouting.Break,
            NotificationRouting.WindDown, NotificationRouting.Focus, NotificationRouting.Hearing,
            NotificationRouting.Hint,
        };

        /// <summary>The alert to show (a <see cref="NotificationRouting"/> action), or null for no request.</summary>
        public static string? Parse(string[] args, string? modeVariable)
        {
            if (modeVariable != "1") return null;

            for (var i = 0; i < args.Length - 1; i++)
            {
                if (!args[i].Equals(SwitchName, StringComparison.OrdinalIgnoreCase)) continue;
                var name = args[i + 1].Trim();
                foreach (var alert in Alerts)
                    if (alert.Equals(name, StringComparison.OrdinalIgnoreCase)) return alert;
                return null;
            }
            return null;
        }
    }
}
