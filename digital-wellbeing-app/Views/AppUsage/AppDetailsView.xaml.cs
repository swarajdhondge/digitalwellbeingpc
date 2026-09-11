using System.Windows;
using System.Windows.Controls;
using digital_wellbeing_app.ViewModels;

namespace digital_wellbeing_app.Views.AppUsage
{
    public partial class AppDetailsView : System.Windows.Controls.UserControl
    {
        public AppDetailsView()
        {
            InitializeComponent();
            digital_wellbeing_app.Helpers.PulseLayout.CapCenter(PageScroll, PageRoot);
        }

        private void EditLimit_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is AppDetailsViewModel vm && !string.IsNullOrEmpty(vm.ExecutablePath))
            {
                var shell = Window.GetWindow(this) as MainWindow.MainWindow;
                shell?.NavigateToAppLimitsForApp(vm.ExecutablePath);
            }
        }
    }
}
