using System;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;
using digital_wellbeing_app.Views.AppUsage;
using digital_wellbeing_app.Views.Dashboard;
using digital_wellbeing_app.Views.Screen;
using digital_wellbeing_app.Views.Settings;
using digital_wellbeing_app.Views.Sound;
using MaterialDesignThemes.Wpf;

namespace digital_wellbeing_app.MainWindow
{
    public partial class MainWindow : Window
    {
        private NotifyIcon? _trayIcon;
        private readonly DashboardView _dashboardView = new();
        private readonly ScreenView _screenView = new();
        private readonly SoundTimelineView _soundView = new();
        private readonly AppUsageView _appUsageView = new();
        private readonly SettingsView _settingsView = new();

        // Track active nav button
        private System.Windows.Controls.Button? _activeNavButton;

        public MainWindow()
        {
            InitializeComponent();
            InitTrayIcon();
            MainContent.Content = _dashboardView;
            _activeNavButton = NavDashboard;

            // Handle maximize state for proper corner radius
            StateChanged += MainWindow_StateChanged;
        }

        #region Tray Icon

        private void InitTrayIcon()
        {
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
                Icon = new Icon(iconPath),
                Visible = true,
                Text = "Digital Wellbeing"
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

        #endregion

        #region Window Chrome - Title Bar

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // Double-click to maximize/restore
                ToggleMaximize();
            }
            else
            {
                // Drag to move
                if (WindowState == WindowState.Maximized)
                {
                    // Restore before dragging from maximized
                    var point = PointToScreen(e.GetPosition(this));
                    WindowState = WindowState.Normal;
                    Left = point.X - (ActualWidth / 2);
                    Top = point.Y - 20;
                }
                DragMove();
            }
        }

        private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            // Handle window snapping (already handled by DragMove)
        }

        #endregion

        #region Window Controls

        private void MinimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            if (WindowState == WindowState.Maximized)
            {
                WindowState = WindowState.Normal;
            }
            else
            {
                WindowState = WindowState.Maximized;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Maximized)
            {
                // Remove corner radius and margin when maximized
                WindowBorder.CornerRadius = new CornerRadius(0);
                WindowBorder.Margin = new Thickness(0);
                MaximizeIcon.Kind = PackIconKind.WindowRestore;
            }
            else
            {
                // Restore corner radius and margin
                WindowBorder.CornerRadius = new CornerRadius(8);
                WindowBorder.Margin = new Thickness(10);
                MaximizeIcon.Kind = PackIconKind.WindowMaximize;
            }
        }

        #endregion

        #region Window State & Closing

        private void Window_StateChanged(object? sender, EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                Hide();
        }

        private void Window_Closing(object? sender, CancelEventArgs e)
        {
            _trayIcon?.Dispose();
        }

        #endregion

        #region Navigation

        private void SetActiveNav(System.Windows.Controls.Button button)
        {
            // Remove active state from previous
            if (_activeNavButton != null)
                _activeNavButton.Tag = null;

            // Set active state on new button
            button.Tag = "Active";
            _activeNavButton = button;
        }

        /// <summary>
        /// Navigate to a new page with fade transition animation
        /// </summary>
        private void NavigateWithTransition(object newContent, System.Windows.Controls.Button navButton)
        {
            // Skip if already on this page
            if (MainContent.Content == newContent) return;

            SetActiveNav(navButton);

            try
            {
                // Get the storyboard from resources
                if (TryFindResource("PageFadeIn") is Storyboard fadeIn)
                {
                    // Set initial state for animation
                    MainContent.Opacity = 0;

                    // Change content
                    MainContent.Content = newContent;

                    // Play fade-in animation
                    fadeIn.Begin(MainContent);
                }
                else
                {
                    // Fallback: just change content without animation
                    MainContent.Content = newContent;
                }
            }
            catch
            {
                // Fallback on any error: just change content
                MainContent.Content = newContent;
                MainContent.Opacity = 1;
            }
        }

        private void Dashboard_Click(object? sender, RoutedEventArgs e)
        {
            NavigateWithTransition(_dashboardView, NavDashboard);
        }

        private void ScreenTime_Click(object? sender, RoutedEventArgs e)
        {
            NavigateWithTransition(_screenView, NavScreen);
        }

        private void Sound_Click(object? sender, RoutedEventArgs e)
        {
            NavigateWithTransition(_soundView, NavSound);
        }

        private void AppUsage_Click(object? sender, RoutedEventArgs e)
        {
            NavigateWithTransition(_appUsageView, NavApps);
        }

        private void Settings_Click(object? sender, RoutedEventArgs e)
        {
            NavigateWithTransition(_settingsView, NavSettings);
        }

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        #endregion
    }
}
