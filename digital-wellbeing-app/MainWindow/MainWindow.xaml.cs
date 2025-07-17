using System;
using System.ComponentModel;
using System.Drawing;                     // for Icon
using System.Windows;
using System.Windows.Controls.Primitives; // for ToggleButton events
using System.Windows.Forms;               // for NotifyIcon
using digital_wellbeing_app.Views.AppUsage;
using digital_wellbeing_app.Views.Dashboard;
using digital_wellbeing_app.Views.Screen;
using digital_wellbeing_app.Views.Settings;
using digital_wellbeing_app.Views.Sound;

namespace digital_wellbeing_app.MainWindow
{
    public partial class MainWindow : Window
    {
        private NotifyIcon? _trayIcon;
        // your views…
        private readonly DashboardView _dashboardView = new();
        private readonly ScreenView _screenView = new();
        private readonly SoundTimelineView _soundView = new();
        private readonly AppUsageView _appUsageView = new();
        private readonly SettingsView _settingsView = new();

        public MainWindow()
        {
            InitializeComponent();
            InitTrayIcon();
            MainContent.Content = _dashboardView;
        }

        private void InitTrayIcon()
        {
            string iconPath = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "Resources", "Icons", "digital-balance-icon.ico"
            );

            if (!System.IO.File.Exists(iconPath))
            {
                // Fully-qualify the WPF MessageBox
                System.Windows.MessageBox.Show(
                    $"Tray icon not found:\n{iconPath}",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                return;
            }

            _trayIcon = new NotifyIcon
            {
                Icon = new Icon(iconPath),
                Visible = true,
                Text = "Digital Balance"
            };

            _trayIcon.ContextMenuStrip = new ContextMenuStrip();
            _trayIcon.ContextMenuStrip.Items.Add("Open", null, (s, e) => ShowWindow());
            _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => Close());
            _trayIcon.DoubleClick += (s, e) => ShowWindow();
        }

        // rail collapse
        private void Hamburger_Checked(object sender, RoutedEventArgs e) =>
            NavColumn.Width = new GridLength(48);

        private void Hamburger_Unchecked(object sender, RoutedEventArgs e) =>
            NavColumn.Width = new GridLength(160);

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                Hide();
        }

        private void Window_Closing(object? sender, CancelEventArgs e) =>
            _trayIcon?.Dispose();

        private void Dashboard_Click(object? sender, RoutedEventArgs e) =>
            MainContent.Content = _dashboardView;

        private void ScreenTime_Click(object? sender, RoutedEventArgs e) =>
            MainContent.Content = _screenView;

        private void Sound_Click(object? sender, RoutedEventArgs e) =>
            MainContent.Content = _soundView;

        private void AppUsage_Click(object? sender, RoutedEventArgs e) =>
            MainContent.Content = _appUsageView;

        
        private void Settings_Click(object? sender, RoutedEventArgs e) =>
            MainContent.Content = _settingsView;

        private void Exit_Click(object? sender, RoutedEventArgs e) =>
            Close();
    }
}
