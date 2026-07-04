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
                key.SetValue(AppName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
    }
}
