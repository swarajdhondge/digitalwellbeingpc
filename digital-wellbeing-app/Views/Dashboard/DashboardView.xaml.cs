// File: Views/Dashboard/DashboardView.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace digital_wellbeing_app.Views.Dashboard
{
    public partial class DashboardView : System.Windows.Controls.UserControl
    {
        public DashboardView()
        {
            InitializeComponent();
            DataContext = new digital_wellbeing_app.ViewModels.DashboardViewModel();
        }

        private void WeeklySummary_Click(object sender, MouseButtonEventArgs e)
        {
            // Navigate to Weekly Report view via MainWindow
            var mainWindow = Window.GetWindow(this) as MainWindow.MainWindow;
            if (mainWindow != null)
            {
                // Find and click the Reports nav button
                var reportsButton = mainWindow.FindName("NavReports") as System.Windows.Controls.Button;
                reportsButton?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            }
        }

        private void SetGoal_Click(object sender, RoutedEventArgs e)
        {
            // Navigate to Settings view to set the goal
            var mainWindow = Window.GetWindow(this) as MainWindow.MainWindow;
            if (mainWindow != null)
            {
                // Find and click the Settings nav button
                var settingsButton = mainWindow.FindName("NavSettings") as System.Windows.Controls.Button;
                settingsButton?.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Button.ClickEvent));
            }
        }
    }
}
