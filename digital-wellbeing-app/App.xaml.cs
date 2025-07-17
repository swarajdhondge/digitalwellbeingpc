// File: App.xaml.cs
using MaterialDesignThemes.Wpf;

namespace digital_wellbeing_app
{
    public partial class App : System.Windows.Application
    {
        public CoreLogic.ScreenTimeTracker ScreenTracker { get; private set; } = null!;
        public CoreLogic.AppUsageTracker AppTracker { get; private set; } = null!;
        private Services.SoundMonitoringService? _soundService;

        // Restore this so your SoundTimelineViewModel can grab it:
        public CoreLogic.SoundExposureManager SoundExposureMgr { get; } = new();

        protected override void OnStartup(System.Windows.StartupEventArgs e)
        {
            base.OnStartup(e);

            // Apply whatever theme was last saved (or system/default for Auto)
            ApplyMaterialTheme();

            // Initialize database & trackers
            Services.DatabaseService.GetConnection();
            ScreenTracker = new CoreLogic.ScreenTimeTracker();
            ScreenTracker.Start();
            AppTracker = new CoreLogic.AppUsageTracker();
            AppTracker.Start();
            _soundService = new Services.SoundMonitoringService();
        }

        /// <summary>
        /// Reads your saved theme (Light/Dark/Auto) via ThemeService,
        /// falls back to system if Auto, then updates MaterialDesign's palette.
        /// </summary>
        public void ApplyMaterialTheme()
        {
            var svc = new Services.ThemeService();
            var mode = svc.Load();  // AppTheme.Light, Dark, or Auto

            bool dark = mode switch
            {
                ViewModels.AppTheme.Light => false,
                ViewModels.AppTheme.Dark => true,
                _ => svc.IsSystemInDarkMode()
            };

            var paletteHelper = new MaterialDesignThemes.Wpf.PaletteHelper();
            var theme = paletteHelper.GetTheme();

            // Your accent picks
            var teal500 = (System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString("#009688");
            var cyan500 = (System.Windows.Media.Color)
                System.Windows.Media.ColorConverter.ConvertFromString("#00BCD4");

            theme.SetPrimaryColor(dark ? cyan500 : teal500);
            theme.SetSecondaryColor(dark ? teal500 : cyan500);
            theme.SetBaseTheme(dark
                ? MaterialDesignThemes.Wpf.BaseTheme.Dark
                : MaterialDesignThemes.Wpf.BaseTheme.Light);

            paletteHelper.SetTheme(theme);
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
