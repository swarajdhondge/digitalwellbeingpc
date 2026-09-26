using System;
using System.IO;
using System.Reflection;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.Tests
{
    /// <summary>
    /// Base class for any test that touches the SQLite store — directly via <c>DatabaseService</c>
    /// or indirectly through <c>ScreenTimeTracker</c> / the live-usage providers. xUnit constructs a
    /// fresh instance per test, so this constructor gives every test a brand-new empty database and
    /// settings directory.
    ///
    /// Without it the whole suite shares one DB (see <see cref="TestSetup"/>) and tests become
    /// order-dependent: e.g. a <c>ScreenTimePeriod</c> row written for "today" without a
    /// <c>SessionStartTime</c> by one test made <c>ScreenTimeTracker</c>'s constructor throw when a
    /// later test built a tracker. Per-test isolation removes that entire class of flakiness.
    /// </summary>
    public abstract class TestBase
    {
        protected TestBase()
        {
            var root = Path.Combine(Path.GetTempPath(), "pulse-tests",
                                    Guid.NewGuid().ToString("N"), "Pulse");
            Directory.CreateDirectory(root);
            DatabaseService.SetDatabasePathForTesting(Path.Combine(root, "test_wellbeing.db"));
            SettingsService.FolderOverride = root;
        }

        protected static void SetField(object target, string field, object? value)
            => target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);

        protected static void CallPrivate(object target, string method, params object?[] args)
            => target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(target, args);
    }
}
