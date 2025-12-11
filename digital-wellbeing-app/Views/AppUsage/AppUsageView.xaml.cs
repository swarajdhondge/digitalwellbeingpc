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
    }
}
