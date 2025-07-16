// File: Services/StartupService.cs
namespace digital_wellbeing_app.Services
{
    public static class StartupService
    {
        private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "Digital Wellbeing";

        public static bool IsEnabled()
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return (key?.GetValue(AppName) as string) != null;
        }

        public static void Enable(bool enable)
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
            if (enable)
            {
                var exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
                key.SetValue(AppName, $"\"{exe}\"");
            }
            else
            {
                key.DeleteValue(AppName, throwOnMissingValue: false);
            }
        }
    }
}
