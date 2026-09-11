using System.Windows;
using System.Windows.Controls;
using digital_wellbeing_app.ViewModels;

namespace digital_wellbeing_app.Views.AppUsage
{
    public partial class AppUsageView : System.Windows.Controls.UserControl
    {
        public AppUsageView()
        {
            InitializeComponent();
            digital_wellbeing_app.Helpers.PulseLayout.CapCenter(PageScroll, PageRoot);

            WeekNavigator.PreviousRequested += (_, _) => (DataContext as AppUsageViewModel)?.GoToPreviousWeek();
            WeekNavigator.NextRequested += (_, _) => (DataContext as AppUsageViewModel)?.GoToNextWeek();
            WeekNavigator.WeekSelected += (_, week) => (DataContext as AppUsageViewModel)?.GoToWeek(week);

            // Refresh only while visible (the view is reused across navigation, so don't dispose
            // the VM here — just stop its timer; disposing broke live updates on re-navigation).
            Loaded += (s, e) => (DataContext as AppUsageViewModel)?.StartRefreshing();
            Unloaded += (s, e) => (DataContext as AppUsageViewModel)?.StopRefreshing();
            IsVisibleChanged += (s, e) =>
            {
                if (DataContext is not AppUsageViewModel vm) return;
                if ((bool)e.NewValue) vm.StartRefreshing();
                else vm.StopRefreshing();
            };
        }

        // Today/Week segmented toggle. (Today's Checked can fire during InitializeComponent,
        // before the DataContext is assigned — the null-guard makes that a no-op.)
        private void TodayRange_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is AppUsageViewModel vm) vm.SetWeekView(false);
        }

        private void WeekRange_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is AppUsageViewModel vm) vm.SetWeekView(true);
        }

        private void HistoryRange_Checked(object sender, RoutedEventArgs e)
        {
            if (DataContext is AppUsageViewModel vm) vm.SetDayView();
        }

        private void PreviousDay_Click(object sender, RoutedEventArgs e)
            => (DataContext as AppUsageViewModel)?.GoToPreviousDay();

        private void NextDay_Click(object sender, RoutedEventArgs e)
            => (DataContext as AppUsageViewModel)?.GoToNextDay();

        // P1-1: Clicking an app row navigates to its App Details
        private void AppRow_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is AppUsageSummary app)
            {
                var shell = Window.GetWindow(this) as MainWindow.MainWindow;
                shell?.NavigateToAppDetails(app.ExecutablePath);
            }
        }

        // P2-3: Clicking the date label opens the hidden DatePicker's calendar popup
        private void DateLabel_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (DataContext is AppUsageViewModel vm)
                HistoryDatePicker.SelectedDate = vm.SelectedDate;
            HistoryDatePicker.IsDropDownOpen = true;
        }

        // P2-3: When the user picks a date from the calendar, navigate to that day
        private void HistoryDatePicker_SelectedDateChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (HistoryDatePicker.SelectedDate is DateTime picked && DataContext is AppUsageViewModel vm)
            {
                vm.GoToHistoryDate(picked);
            }
        }

        // P3-3: CTA on empty state
        private void EmptyStateDashboard_Click(object sender, RoutedEventArgs e)
        {
            var shell = Window.GetWindow(this) as MainWindow.MainWindow;
            shell?.NavigateToDashboard();
        }

    }
}
