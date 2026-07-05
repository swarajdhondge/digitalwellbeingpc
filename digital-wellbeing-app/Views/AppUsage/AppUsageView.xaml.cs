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
            Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            // Dispose ViewModel to stop timer and unsubscribe events
            if (DataContext is AppUsageViewModel vm)
            {
                vm.Dispose();
            }
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
    }
}
