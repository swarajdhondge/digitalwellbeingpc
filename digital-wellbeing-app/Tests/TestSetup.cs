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
    /// Redirects DatabaseService, SettingsService, and LogService to a throwaway temp directory
    /// before any test runs, so the suite never reads or writes the user's real Pulse data. Runs
    /// once when the test assembly is loaded. LogService redirection added 2026-07-17: it was the
    /// only one of the three without isolation, so every test run - including ones that
    /// deliberately trigger a Warning/Error path, e.g. DatabaseService's write-side validation
    /// rejections - wrote into the user's real log file.
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

            LogService.FolderOverride = root;
            LogService.ResetForTesting();
            LogService.Initialize();
        }
    }
}
