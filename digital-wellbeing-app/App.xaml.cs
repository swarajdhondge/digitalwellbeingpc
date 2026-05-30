// File: App.xaml.cs
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32;
using Velopack;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using SkiaSharp;

namespace digital_wellbeing_app
{
    public partial class App : System.Windows.Application
    {
        /// <summary>
        /// Configures LiveCharts2 global theme settings
        /// </summary>
        public static void ConfigureLiveChartsTheme(bool isDark)
        {
            // Define colors based on theme - matches our design tokens
            var textColor = isDark 
                ? SKColor.Parse("#94A3B8")   // Slate 400 (Text.Secondary)
                : SKColor.Parse("#64748B");  // Slate 500
                
            var accentPrimary = SKColor.Parse("#2DD4BF");    // Teal 400 (Accent.Primary)
            var statusSuccess = SKColor.Parse("#22C55E");    // Green 500
            var statusWarning = SKColor.Parse("#F59E0B");    // Amber 500
            var statusDanger = SKColor.Parse("#EF4444");     // Red 500
            var statusInfo = SKColor.Parse("#3B82F6");       // Blue 500

            LiveCharts.Configure(config => config
                .AddSkiaSharp()
                .AddDefaultMappers()
                .AddDarkTheme()  // Start with dark as base, we customize below
                .HasGlobalSKTypeface(SKFontManager.Default.MatchFamily("Segoe UI"))
            );
        }

        // User-scoped mutex: allows multiple Windows users to each run their own instance
        private static readonly string MutexName = $"DigitalWellbeingPC_SingleInstance_{GetCurrentUserSid()}";
        private static Mutex? _mutex;

        private static string GetCurrentUserSid()
        {
            try
            {
                return WindowsIdentity.GetCurrent().User?.Value ?? "default";
            }
            catch
            {
                return "default";
            }
        }

        public CoreLogic.ScreenTimeTracker ScreenTracker { get; private set; } = null!;
        public CoreLogic.AppUsageTracker AppTracker { get; private set; } = null!;
        private Services.SoundMonitoringService? _soundService;

        public CoreLogic.SoundExposureManager SoundExposureMgr { get; } = new();

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            // Velopack update hooks - must be first
            VelopackApp.Build().Run();

            // Global exception handlers - catch unhandled crashes
            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

            // Initialize logging
            Services.LogService.Initialize();
            Services.LogService.Info("App starting up");

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

            // Configure LiveCharts2 theme to match
            bool isDark = savedMode switch
            {
                ViewModels.AppTheme.Light => false,
                ViewModels.AppTheme.Dark => true,
                _ => themeService.IsSystemInDarkMode()
            };
            ConfigureLiveChartsTheme(isDark);

            // Initialize database & trackers
            Services.DatabaseService.GetConnection();
            ScreenTracker = new CoreLogic.ScreenTimeTracker();
            ScreenTracker.Start();
            AppTracker = new CoreLogic.AppUsageTracker();
            AppTracker.Start();
            _soundService = new Services.SoundMonitoringService(SoundExposureMgr);

            Services.LogService.Info("Trackers started successfully");

            // Subscribe to system events for pause/resume tracking
            SystemEvents.SessionSwitch += OnSessionSwitch;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            // Auto-check for updates (non-blocking, fire-and-forget)
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    // Delay to let UI finish loading first
                    await System.Threading.Tasks.Task.Delay(5000);
                    var updateService = new Services.UpdateService();
                    await Current.Dispatcher.InvokeAsync(async () =>
                    {
                        await updateService.CheckAndPromptUpdateAsync();
                    });
                }
                catch (System.Exception ex)
                {
                    Services.LogService.Warning($"Auto-update check failed: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Handles unhandled exceptions on the UI dispatcher thread.
        /// Shows a user-friendly error dialog and logs the crash.
        /// </summary>
        private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            Services.LogService.Error("Unhandled UI exception", e.Exception);

            try
            {
                System.Windows.MessageBox.Show(
                    $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n" +
                    "The error has been logged. The application will try to continue.\n" +
                    "If the problem persists, please restart the application.",
                    "Digital Wellbeing - Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            catch
            {
                // If we can't show a dialog, just log and continue
            }

            e.Handled = true; // Prevent app crash for recoverable exceptions
        }

        /// <summary>
        /// Handles unhandled exceptions on non-UI threads (fatal).
        /// </summary>
        private void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Services.LogService.Error("Fatal unhandled exception", ex);
            }
        }

        /// <summary>
        /// Handles unobserved Task exceptions (prevents silent failures).
        /// </summary>
        private void OnUnobservedTaskException(object? sender, System.Threading.Tasks.UnobservedTaskExceptionEventArgs e)
        {
            Services.LogService.Error("Unobserved task exception", e.Exception);
            e.SetObserved(); // Prevent process termination
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
            Services.LogService.Info("App shutting down");

            // Unsubscribe from system events
            SystemEvents.SessionSwitch -= OnSessionSwitch;
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;

            ScreenTracker?.Stop();
            ScreenTracker?.Dispose();
            AppTracker?.Stop();
            AppTracker?.Dispose();
            SoundExposureMgr?.Dispose();
            _soundService?.Dispose();

            // Close database connection last (after all trackers have saved)
            Services.DatabaseService.CloseConnection();

            Services.LogService.Info("App shutdown complete");

            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
