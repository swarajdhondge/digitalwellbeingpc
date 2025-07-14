using System.Windows;
using Media = System.Windows.Media;
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
        public SoundExposureManager SoundExposureMgr { get; } = new();
        public SoundTimelineViewModel SoundTimelineVm { get; } = new();

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            ApplyMaterialTheme();

            DatabaseService.GetConnection();
            ScreenTracker = new ScreenTimeTracker();
            ScreenTracker.Start();
            AppTracker = new AppUsageTracker();
            AppTracker.Start();
            _soundService = new SoundMonitoringService();
        }

        public void ApplyMaterialTheme()
        {
            var svc = new ThemeService();
            var mode = svc.Load();
            bool dark = mode switch
            {
                AppTheme.Light => false,
                AppTheme.Dark => true,
                _ => svc.IsSystemInDarkMode()
            };

            var palette = new PaletteHelper();
            var theme = palette.GetTheme();

            var teal500 = (Media.Color)Media.ColorConverter.ConvertFromString("#009688");
            var cyan500 = (Media.Color)Media.ColorConverter.ConvertFromString("#00BCD4");

            theme.SetPrimaryColor(dark ? cyan500 : teal500);
            theme.SetSecondaryColor(dark ? teal500 : cyan500);
            theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);

            palette.SetTheme(theme);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            ScreenTracker.Stop();
            AppTracker.Stop();
            _soundService?.Dispose();
            base.OnExit(e);
        }
    }
}
