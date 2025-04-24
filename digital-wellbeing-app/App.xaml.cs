using System.Windows;
using digital_wellbeing_app.CoreLogic;

namespace digital_wellbeing_app
{
    public partial class App : Application
    {
        // this must match what ScreenView.xaml.cs expects
        public ScreenTimeTracker ScreenTracker { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // create & start the one-and-only tracker
            ScreenTracker = new ScreenTimeTracker();
            ScreenTracker.Start();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // flush on app shutdown
            ScreenTracker.Stop();
            base.OnExit(e);
        }
    }
}
