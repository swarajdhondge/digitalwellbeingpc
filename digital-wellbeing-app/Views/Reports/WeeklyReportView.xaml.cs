namespace digital_wellbeing_app.Views.Reports
{
    /// <summary>
    /// Interaction logic for WeeklyReportView.xaml
    /// </summary>
    public partial class WeeklyReportView : System.Windows.Controls.UserControl
    {
        public WeeklyReportView()
        {
            InitializeComponent();
            digital_wellbeing_app.Helpers.PulseLayout.CapCenter(PageScroll, PageRoot);

            WeekNavigator.PreviousRequested += (_, _) => (DataContext as digital_wellbeing_app.ViewModels.WeeklyReportViewModel)?.GoToPreviousWeek();
            WeekNavigator.NextRequested += (_, _) => (DataContext as digital_wellbeing_app.ViewModels.WeeklyReportViewModel)?.GoToNextWeek();
            WeekNavigator.WeekSelected += (_, week) => (DataContext as digital_wellbeing_app.ViewModels.WeeklyReportViewModel)?.GoToWeek(week);

            // Reload whenever the page is opened; only the current week changes while it stays open.
            var refreshTimer = new System.Windows.Threading.DispatcherTimer { Interval = System.TimeSpan.FromMinutes(1) };
            refreshTimer.Tick += (_, _) =>
            {
                if (DataContext is digital_wellbeing_app.ViewModels.WeeklyReportViewModel { CanNavigateNext: false } vm)
                    vm.Refresh();
            };
            Loaded += (_, _) =>
            {
                (DataContext as digital_wellbeing_app.ViewModels.WeeklyReportViewModel)?.Refresh();
                refreshTimer.Start();
            };
            Unloaded += (_, _) => refreshTimer.Stop();
        }
    }
}

