// File: Services/StartupService.cs
using System.Threading.Tasks;

namespace digital_wellbeing_app.Services
{
    /// <summary>
    /// Run-at-startup control. The classic (Velopack) build uses an HKCU Run key; the Store
    /// (packaged) build must use the MSIX <c>windows.startupTask</c> extension instead, because
    /// the Run key is virtualized under the package and would not persist. Callers should prefer
    /// the async members, which route to the correct mechanism for the current build.
    /// </summary>
    public static class StartupService
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "Pulse";
        private const string StartupTaskId = "PulseStartupId"; // matches Package.appxmanifest

        /// <summary>Set during App.OnStartup so the window can start directly in the tray.</summary>
        public static bool IsStartupLaunch { get; private set; }

        public static void DetectStartupLaunch(string[] args)
        {
            IsStartupLaunch = args.Any(arg =>
                arg.Equals("--startup", System.StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("-startup", System.StringComparison.OrdinalIgnoreCase) ||
                arg.Contains("StartupTask", System.StringComparison.OrdinalIgnoreCase));

            if (!IsStartupLaunch && PackagedAppInfo.IsPackaged)
            {
                try
                {
                    var activation = Windows.ApplicationModel.AppInstance.GetActivatedEventArgs();
                    IsStartupLaunch = activation?.Kind == Windows.ApplicationModel.Activation.ActivationKind.StartupTask;
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Could not inspect startup activation: {ex.Message}");
                }
            }
        }

        /// <summary>Migrates an existing classic Run entry to include the silent-start marker.</summary>
        public static void EnsureStartupArguments()
        {
            if (PackagedAppInfo.IsPackaged) return;
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key?.GetValue(AppName) is not string value)
                return;

            var exe = System.Environment.ProcessPath
                      ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
            var desired = $"\"{exe}\" --startup";

            // Keep enabled entries pointed at the currently running installation. This matters
            // after switching from a development build to Velopack, or after an install moves.
            if (!value.Equals(desired, System.StringComparison.OrdinalIgnoreCase))
                key.SetValue(AppName, desired);
        }

        /// <summary>Whether launch-at-startup is currently enabled (routes by build type).</summary>
        public static async Task<bool> IsEnabledAsync()
        {
            if (PackagedAppInfo.IsPackaged)
            {
                try
                {
                    var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
                    return task.State == Windows.ApplicationModel.StartupTaskState.Enabled
                        || task.State == Windows.ApplicationModel.StartupTaskState.EnabledByPolicy;
                }
                catch
                {
                    return false;
                }
            }
            return IsEnabled();
        }

        /// <summary>Enable/disable launch-at-startup (routes by build type).</summary>
        public static async Task SetEnabledAsync(bool enable)
        {
            if (PackagedAppInfo.IsPackaged)
            {
                try
                {
                    var task = await Windows.ApplicationModel.StartupTask.GetAsync(StartupTaskId);
                    if (enable)
                        await task.RequestEnableAsync();
                    else
                        task.Disable();
                }
                catch (System.Exception ex)
                {
                    LogService.Warning($"StartupTask toggle failed: {ex.Message}");
                }
                return;
            }
            Enable(enable);
        }

        // --- Classic (unpackaged) HKCU Run key implementation ---

        public static bool IsEnabled()
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return (key?.GetValue(AppName) as string) != null;
        }

        public static void Enable(bool enable)
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (key == null) return;

            if (enable)
            {
                var exe = System.Environment.ProcessPath
                          ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
                key.SetValue(AppName, $"\"{exe}\" --startup");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
    }
}
