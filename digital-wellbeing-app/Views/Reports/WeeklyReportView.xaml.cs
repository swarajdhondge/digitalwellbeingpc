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
        }

        // P1-4: Clicking a top app navigates to its App Details
        private void TopApp_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((sender as System.Windows.FrameworkElement)?.DataContext is digital_wellbeing_app.Models.AppUsageSummary app)
            {
                var shell = System.Windows.Window.GetWindow(this) as MainWindow.MainWindow;
                shell?.NavigateToAppDetails(app.ExecutablePath);
            }
        }

        private void Chart_DataPointerDown(LiveChartsCore.Kernel.Sketches.IChartView chart, System.Collections.Generic.IEnumerable<LiveChartsCore.Kernel.ChartPoint> points)
        {
            var point = System.Linq.Enumerable.FirstOrDefault(points);
            if (point?.Context.DataSource is digital_wellbeing_app.Models.DailyScreenTime data)
            {
                var shell = System.Windows.Window.GetWindow(this) as MainWindow.MainWindow;
                shell?.NavigateToAppUsageHistory(data.Date);
            }
        }
    }
}
