// File: App.xaml.cs
using System.Threading;
using Microsoft.Win32;

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

            // Subscribe to system events for pause/resume tracking
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
        }

        /// <summary>
        /// Handles session switch events (lock/unlock, logoff, etc.)
        /// </summary>
        private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                case SessionSwitchReason.SessionLogoff:
                case SessionSwitchReason.ConsoleDisconnect:
                case SessionSwitchReason.RemoteDisconnect:
                    // Screen locked or user logged off - pause tracking
                    ScreenTracker?.Pause();
                    AppTracker?.Stop();
                    break;

                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.SessionLogon:
                case SessionSwitchReason.ConsoleConnect:
                case SessionSwitchReason.RemoteConnect:
                    // Screen unlocked or user logged on - resume tracking
                    ScreenTracker?.Resume();
                    AppTracker?.Start();
                    break;
            }
        }

        /// <summary>
        /// Handles power mode changes (sleep, hibernate, resume)
        /// </summary>
        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    // PC going to sleep/hibernate - pause tracking
                    ScreenTracker?.Pause();
                    AppTracker?.Stop();
                    break;

                case PowerModes.Resume:
                    // PC waking up - resume tracking
                    ScreenTracker?.Resume();
                    AppTracker?.Start();
                    break;
            }
        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            // Unsubscribe from system events
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            ScreenTracker?.Stop();
            AppTracker?.Stop();
            _soundService?.Dispose();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
