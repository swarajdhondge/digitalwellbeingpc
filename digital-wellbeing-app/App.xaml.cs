using System.Windows;
using digital_wellbeing_app.CoreLogic;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app
{
    public partial class App : Application
    {
        public ScreenTimeTracker ScreenTracker { get; private set; } = null!;
        public AppUsageTracker AppTracker { get; private set; } = null!;

        private SoundMonitoringService? _soundService;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DatabaseService.GetConnection();

            ScreenTracker = new ScreenTimeTracker();
            ScreenTracker.Start();

            AppTracker = new AppUsageTracker();
            AppTracker.Start();

            _soundService = new SoundMonitoringService();
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
