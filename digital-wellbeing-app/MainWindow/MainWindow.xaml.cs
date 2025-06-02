using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Forms;         
using System.Drawing;               
using digital_wellbeing_app.Views.Dashboard;
using digital_wellbeing_app.Views.Screen;
using digital_wellbeing_app.Views.Sound;
using digital_wellbeing_app.Views.AppUsage;

namespace digital_wellbeing_app.MainWindow
{
    public partial class MainWindow : Window
    {
        private NotifyIcon? _trayIcon;

        // Instantiate your four views once
        private readonly DashboardView _dashboardView = new();
        private readonly ScreenView _screenView = new();
        private readonly SoundTimelineView _soundView = new();
        private readonly AppUsageView _appUsageView = new();

        public MainWindow()
        {
            InitializeComponent();
            InitTrayIcon();

            // Show Dashboard by default
            MainContent.Content = _dashboardView;
        }

        private void InitTrayIcon()
        {
            // Build path to the .ico in your output folder
            string iconPath = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "Resources", "Icons", "digital-balance-icon.ico"
            );

            if (!System.IO.File.Exists(iconPath))
            {
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
                Icon = new System.Drawing.Icon(iconPath),
                Visible = true,
                Text = "Digital Balance"
            };

            _trayIcon.ContextMenuStrip = new ContextMenuStrip();
            _trayIcon.ContextMenuStrip.Items.Add("Open", null, (s, e) => ShowWindow());
            _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => Close());
            _trayIcon.DoubleClick += (s, e) => ShowWindow();
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
            {
                Hide();
            }
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            _trayIcon?.Dispose();
        }

        private void Dashboard_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = _dashboardView;
        }

        private void ScreenTime_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = _screenView;
        }

        private void Sound_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = _soundView;
        }

        private void AppUsage_Click(object? sender, RoutedEventArgs e)
        {
            MainContent.Content = _appUsageView;
        }

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
