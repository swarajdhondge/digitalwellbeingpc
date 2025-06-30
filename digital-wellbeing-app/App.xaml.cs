using MaterialDesignThemes.Wpf;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.ViewModels;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app
{
    public partial class App : System.Windows.Application
    {
        public ScreenTimeTracker ScreenTracker { get; private set; } = null!;
        public AppUsageTracker AppTracker { get; private set; } = null!;
        private SoundMonitoringService? _soundService;

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            var palette = new PaletteHelper();
            var svc = new ThemeService();
            var mode = svc.Load();
            bool dark = mode switch
            {
                AppTheme.Light => false,
                AppTheme.Dark => true,
                _ => svc.IsSystemInDarkMode()
            };

            var theme = palette.GetTheme();
            theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
            palette.SetTheme(theme);

            // original trackers
            DatabaseService.GetConnection();
            ScreenTracker = new ScreenTimeTracker();
            ScreenTracker.Start();
            AppTracker = new AppUsageTracker();
            AppTracker.Start();
            _soundService = new SoundMonitoringService();
        }

        protected override void OnExit(System.Windows.ExitEventArgs e)
        {
            ScreenTracker.Stop();
            AppTracker.Stop();
            _soundService?.Dispose();
            base.OnExit(e);
        }
    }
}
