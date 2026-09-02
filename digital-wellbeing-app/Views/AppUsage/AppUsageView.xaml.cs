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
    }
}
