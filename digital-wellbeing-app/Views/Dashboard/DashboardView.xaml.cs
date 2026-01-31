// File: Views/Dashboard/DashboardView.xaml.cs
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace digital_wellbeing_app.Views.Dashboard
{
    public partial class DashboardView : System.Windows.Controls.UserControl
    {
        private readonly digital_wellbeing_app.ViewModels.DashboardViewModel _vm;

        public DashboardView()
        {
            InitializeComponent();
            _vm = new digital_wellbeing_app.ViewModels.DashboardViewModel();
            DataContext = _vm;

            Loaded += DashboardView_Loaded;
            Unloaded += DashboardView_Unloaded;
            IsVisibleChanged += DashboardView_IsVisibleChanged;
        }

        private void DashboardView_Loaded(object sender, RoutedEventArgs e)
        {
            _vm.StartRefreshing();
        }

        private void DashboardView_Unloaded(object sender, RoutedEventArgs e)
        {
            _vm.StopRefreshing();
        }

        private void DashboardView_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if ((bool)e.NewValue)
                _vm.StartRefreshing();
            else
                _vm.StopRefreshing();
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
