using System;
using System.IO;
using System.Runtime.CompilerServices;
using digital_wellbeing_app.Services;

// The service layer persists to shared per-user locations (a SQLite DB and settings.json).
// Running tests in parallel let them race on those shared files, which made settings/DB tests
// flaky. Serialize the whole assembly so every test sees a deterministic store.
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace digital_wellbeing_app.Tests
{
    /// <summary>
    /// Redirects DatabaseService and SettingsService to a throwaway temp directory before any
    /// test runs, so the suite never reads or writes the user's real Pulse data. Runs once when
    /// the test assembly is loaded.
    /// </summary>
    internal static class TestSetup
    {
        [ModuleInitializer]
        internal static void Initialize()
        {
            // Keep a "Pulse" folder segment so path-shape assertions still hold under redirection.
            var root = Path.Combine(Path.GetTempPath(), "pulse-tests", Guid.NewGuid().ToString("N"), "Pulse");
            Directory.CreateDirectory(root);

            DatabaseService.SetDatabasePathForTesting(Path.Combine(root, "test_wellbeing.db"));
            SettingsService.FolderOverride = root;
        }
    }
}
