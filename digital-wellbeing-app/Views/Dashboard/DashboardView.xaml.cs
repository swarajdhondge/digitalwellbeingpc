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

        private MainWindow.MainWindow? Shell => Window.GetWindow(this) as MainWindow.MainWindow;

        private void WeeklySummary_Click(object sender, MouseButtonEventArgs e) => Shell?.NavigateToReports();
        private void Hearing_Click(object sender, MouseButtonEventArgs e) => Shell?.NavigateToSound();
        private void MostUsed_Click(object sender, MouseButtonEventArgs e) => Shell?.NavigateToAppUsage();
        private void EnterFocus_Click(object sender, RoutedEventArgs e) => Shell?.NavigateToFocus();
        private void SetGoal_Click(object sender, RoutedEventArgs e) => Shell?.NavigateToSettings();
    }
}
