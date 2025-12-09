// File: App.xaml.cs
using System.Threading;

namespace digital_wellbeing_app
{
    public partial class App : System.Windows.Application
    {
        private const string MutexName = "DigitalWellbeingPC_SingleInstance";
        private static Mutex? _mutex;

        public CoreLogic.ScreenTimeTracker ScreenTracker { get; private set; } = null!;
        public CoreLogic.AppUsageTracker AppTracker { get; private set; } = null!;
        private Services.SoundMonitoringService? _soundService;

        public CoreLogic.SoundExposureManager SoundExposureMgr { get; } = new();

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            // Single instance check
            _mutex = new Mutex(true, MutexName, out bool isNewInstance);
            if (!isNewInstance)
            {
                System.Windows.MessageBox.Show(
                    "Digital Wellbeing is already running.\nCheck the system tray.",
                    "Already Running",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
                Shutdown();
                return;
            }

            base.OnStartup(e);

            // Apply saved theme (swaps palette + MaterialDesign)
            var themeService = new Services.ThemeService();
            var savedMode = themeService.Load();
            themeService.ApplyTheme(savedMode);

            // Initialize database & trackers
            Services.DatabaseService.GetConnection();
            ScreenTracker = new CoreLogic.ScreenTimeTracker();
            ScreenTracker.Start();
            AppTracker = new CoreLogic.AppUsageTracker();
            AppTracker.Start();
            _soundService = new Services.SoundMonitoringService();
        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            ScreenTracker?.Stop();
            AppTracker?.Stop();
            _soundService?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
