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
using digital_wellbeing_app.Views.Focus;
using digital_wellbeing_app.Views.Screen;
using digital_wellbeing_app.Views.Settings;
using digital_wellbeing_app.Views.Sound;
using MaterialDesignThemes.Wpf;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;

namespace digital_wellbeing_app.MainWindow
{
    public partial class MainWindow : Window
    {
        private NotifyIcon? _trayIcon;
        private readonly DashboardView _dashboardView = new();
        private readonly ScreenView _screenView = new();
        private readonly SoundTimelineView _soundView = new();
        private readonly AppUsageView _appUsageView = new();
        private readonly FocusView _focusView = new();
        private readonly SettingsView _settingsView = new();

        // Track active nav button
        private System.Windows.Controls.Button? _activeNavButton;

        // Break reminder service
        private Services.BreakReminderService? _breakReminderService;

        // Focus session service
        private Services.FocusSessionService? _focusSessionService;
        private string _currentDistractingApp = string.Empty;

        // Wind Down service
        private Services.WindDownService? _windDownService;

        public MainWindow()
        {
            InitializeComponent();
            InitTrayIcon();
            InitBreakReminder();
            InitFocusSession();
            InitWindDown();
            MainContent.Content = _dashboardView;
            _activeNavButton = NavDashboard;

            // Handle maximize state for proper corner radius
            StateChanged += MainWindow_StateChanged;
        }

        private void InitFocusSession()
        {
            _focusSessionService = new Services.FocusSessionService();
            _focusSessionService.DistractingAppDetected += OnDistractingAppDetected;
            _focusSessionService.SessionEnded += OnFocusSessionEnded;
        }

        /// <summary>
        /// Get the focus session service for child views
        /// </summary>
        public Services.FocusSessionService? GetFocusSessionService()
        {
            return _focusSessionService;
        }

        private void OnDistractingAppDetected(string appName, string executablePath)
        {
            // Store the app name for when user responds to the warning
            _currentDistractingApp = appName;

            // Show notification (balloon + in-app overlay)
            Dispatcher.Invoke(() =>
            {
                ShowFocusWarningNotification(appName);
            });
        }

        private void ShowFocusWarningNotification(string appName)
        {
            if (_focusSessionService == null) return;

            var remaining = _focusSessionService.TimeRemaining;
            var timeText = $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}";

            // Update the in-app overlay
            DistractingAppName.Text = appName;
            FocusTimeRemaining.Text = $"{timeText} remaining in focus session";

            // Show tray balloon notification (this works without app registration)
            // The balloon plays its own warning sound, so we don't need to play one manually
            if (_trayIcon != null)
            {
                _trayIcon.BalloonTipTitle = "⚠️ Distracting App Detected";
                _trayIcon.BalloonTipText = $"{appName} is marked as Entertainment.\n{timeText} remaining in focus. Click to respond.";
                _trayIcon.BalloonTipIcon = ToolTipIcon.Warning;
                _trayIcon.BalloonTipClicked += OnFocusWarningBalloonClicked;
                _trayIcon.ShowBalloonTip(5000);
            }
        }

        private void OnFocusWarningBalloonClicked(object? sender, EventArgs e)
        {
            // Unsubscribe to prevent multiple handlers
            if (_trayIcon != null)
            {
                _trayIcon.BalloonTipClicked -= OnFocusWarningBalloonClicked;
            }

            // Show the in-app overlay when user clicks the balloon
            Dispatcher.Invoke(() =>
            {
                // Bring window to front and show overlay
                if (WindowState == WindowState.Minimized)
                    WindowState = WindowState.Normal;
                Show();
                Activate();
                
                FocusWarningOverlay.Visibility = Visibility.Visible;
            });
        }

        private void OnFocusSessionEnded(bool completed)
        {
            Dispatcher.Invoke(() =>
            {
                FocusWarningOverlay.Visibility = Visibility.Collapsed;
            });
        }

        #region Wind Down Mode

        private void InitWindDown()
        {
            _windDownService = new Services.WindDownService();
            _windDownService.LoadSettings();
            _windDownService.WindDownStarted += OnWindDownStarted;
            _windDownService.WindDownEnded += OnWindDownEnded;
            _windDownService.Start();

            // Check if we're already in Wind Down mode when the app starts
            if (_windDownService.IsWindDownActive && _windDownService.ShowVisualCue)
            {
                UpdateWindDownVisual(true);
            }
        }

        private void OnWindDownStarted()
        {
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("[WindDown] Wind Down mode started");

                // Show notification if enabled and not shown yet this session
                if (_windDownService?.ShowNotification == true && !_windDownService.HasNotificationBeenShown)
                {
                    ShowWindDownNotification();
                    _windDownService.MarkNotificationShown();
                }

                // Show visual cue if enabled
                if (_windDownService?.ShowVisualCue == true)
                {
                    UpdateWindDownVisual(true);
                }
            });
        }

        private void OnWindDownEnded()
        {
            Dispatcher.Invoke(() =>
            {
                System.Diagnostics.Debug.WriteLine("[WindDown] Wind Down mode ended");
                UpdateWindDownVisual(false);
            });
        }

        private void ShowWindDownNotification()
        {
            if (_trayIcon == null) return;

            _trayIcon.BalloonTipTitle = "🌙 Time to Wind Down";
            _trayIcon.BalloonTipText = "Quiet hours have started. Consider wrapping up and getting ready for rest.";
            _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            _trayIcon.ShowBalloonTip(5000);
        }

        private void UpdateWindDownVisual(bool active)
        {
            if (active && _windDownService?.ShowVisualCue == true)
            {
                // Set border color based on visual style
                var color = _windDownService.VisualStyle switch
                {
                    Models.WindDownVisualStyle.Amber => System.Windows.Media.Color.FromRgb(245, 158, 11),  // #F59E0B
                    Models.WindDownVisualStyle.Purple => System.Windows.Media.Color.FromRgb(139, 92, 246), // #8B5CF6
                    Models.WindDownVisualStyle.Dim => System.Windows.Media.Color.FromRgb(107, 114, 128),   // #6B7280
                    _ => System.Windows.Media.Color.FromRgb(245, 158, 11)
                };

                WindDownBorder.Visibility = Visibility.Visible;
                WindDownBorder.BorderBrush = new System.Windows.Media.SolidColorBrush(color);
                WindDownBorder.Opacity = _windDownService.VisualOpacity;
            }
            else
            {
                WindDownBorder.Visibility = Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Update Wind Down service with new settings (called from SettingsView)
        /// </summary>
        public void UpdateWindDownService()
        {
            if (_windDownService != null)
            {
                _windDownService.Stop();
                _windDownService.LoadSettings();
                _windDownService.Start();

                // Update visual immediately if settings changed
                if (_windDownService.IsWindDownActive && _windDownService.ShowVisualCue)
                {
                    UpdateWindDownVisual(true);
                }
                else
                {
                    UpdateWindDownVisual(false);
                }
            }
        }

        #endregion

        private void BackToWorkButton_Click(object sender, RoutedEventArgs e)
        {
            // Dismiss warning - if user goes back to the same app, warn immediately
            _focusSessionService?.DismissWarning(_currentDistractingApp);
            _currentDistractingApp = string.Empty;
            FocusWarningOverlay.Visibility = Visibility.Collapsed;
            // The distracting app was already minimized by the service (in Block mode)
            // or user is just acknowledging (in Warn mode)
        }

        private void ContinueAnywayButton_Click(object sender, RoutedEventArgs e)
        {
            // Record the override and allow this app for the rest of the session
            _focusSessionService?.RecordDistractionOverride();
            _focusSessionService?.AllowAppForSession(_currentDistractingApp);
            _currentDistractingApp = string.Empty;
            FocusWarningOverlay.Visibility = Visibility.Collapsed;
        }

        private void FocusBackdrop_Click(object sender, MouseButtonEventArgs e)
        {
            // Clicking backdrop = back to work (don't allow the app)
            _focusSessionService?.DismissWarning(_currentDistractingApp);
            _currentDistractingApp = string.Empty;
            FocusWarningOverlay.Visibility = Visibility.Collapsed;
        }

        #region End Session Confirmation

        /// <summary>
        /// Show the custom end session confirmation dialog
        /// </summary>
        public void ShowEndSessionConfirmation()
        {
            if (_focusSessionService == null || !_focusSessionService.IsInFocusMode)
            {
                return;
            }

            var remaining = _focusSessionService.TimeRemaining;
            var timeText = $"{(int)remaining.TotalMinutes}:{remaining.Seconds:D2}";
            EndSessionMessage.Text = $"You still have {timeText} remaining. Are you sure you want to end early?";
            EndSessionOverlay.Visibility = Visibility.Visible;
        }

        private void KeepGoingButton_Click(object sender, RoutedEventArgs e)
        {
            EndSessionOverlay.Visibility = Visibility.Collapsed;
        }

        private void ConfirmEndSessionButton_Click(object sender, RoutedEventArgs e)
        {
            EndSessionOverlay.Visibility = Visibility.Collapsed;
            _focusSessionService?.EndSession(false);
        }

        private void EndSessionBackdrop_Click(object sender, MouseButtonEventArgs e)
        {
            // Clicking backdrop = keep going
            EndSessionOverlay.Visibility = Visibility.Collapsed;
        }

        #endregion

        private void InitBreakReminder()
        {
            _breakReminderService = new Services.BreakReminderService();
            _breakReminderService.LoadSettings();
            _breakReminderService.BreakDue += OnBreakDue;
            _breakReminderService.BreakDismissed += OnBreakDismissed;
            _breakReminderService.Start();
        }

        // Track if break notification is pending (for when user clicks balloon)
        private bool _breakNotificationPending = false;

        private void OnBreakDue()
        {
            System.Diagnostics.Debug.WriteLine("OnBreakDue triggered!");
            // Show break notification on UI thread
            Dispatcher.Invoke(() =>
            {
                // Check if app is visible or minimized/hidden
                bool isMinimized = WindowState == WindowState.Minimized || !IsVisible;
                System.Diagnostics.Debug.WriteLine($"isMinimized: {isMinimized}, WindowState: {WindowState}, IsVisible: {IsVisible}");
                
                if (isMinimized)
                {
                    // Show balloon notification from system tray (balloon has its own sound)
                    _breakNotificationPending = true;
                    ShowFallbackBalloon();
                }
                else
                {
                    // Show centered overlay
                    ShowBreakOverlay();
                    
                    // Play system sound if enabled (only for overlay, balloon has its own)
                    if (_breakReminderService?.SoundEnabled == true)
                    {
                        System.Media.SystemSounds.Asterisk.Play();
                    }
                }
            });
        }

        private ToastNotification? _currentToast;
        private const string APP_ID = "DigitalWellbeing";

        private void ShowToastNotification()
        {
            System.Diagnostics.Debug.WriteLine("ShowToastNotification called - attempting to show toast");
            try
            {
                // Create toast XML with action buttons
                string toastXml = @"
                    <toast activationType='foreground' launch='action=open'>
                        <visual>
                            <binding template='ToastGeneric'>
                                <text>Time for a break!</text>
                                <text>Follow the 20-20-20 rule: Look away from screen for 20 seconds.</text>
                            </binding>
                        </visual>
                        <actions>
                            <action content='Snooze 5 min' arguments='action=snooze' activationType='foreground'/>
                            <action content='Dismiss' arguments='action=dismiss' activationType='foreground'/>
                        </actions>
                        <audio src='ms-winsoundevent:Notification.Default'/>
                    </toast>";

                var xmlDoc = new XmlDocument();
                xmlDoc.LoadXml(toastXml);

                _currentToast = new ToastNotification(xmlDoc);
                
                // Handle toast activation (button clicks or toast click)
                _currentToast.Activated += Toast_Activated;
                _currentToast.Dismissed += Toast_Dismissed;

                // Show the toast
                ToastNotificationManager.CreateToastNotifier(APP_ID).Show(_currentToast);
            }
            catch (Exception ex)
            {
                // Fallback to balloon tip if toast fails
                System.Diagnostics.Debug.WriteLine($"Toast error: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
                ShowFallbackBalloon();
            }
            System.Diagnostics.Debug.WriteLine("ShowToastNotification completed");
        }

        private void Toast_Activated(ToastNotification sender, object args)
        {
            Dispatcher.Invoke(() =>
            {
                _breakNotificationPending = false;
                
                // Parse the arguments to determine action
                string arguments = "";
                if (args is Windows.UI.Notifications.ToastActivatedEventArgs toastArgs)
                {
                    arguments = toastArgs.Arguments;
                }

                if (arguments.Contains("snooze"))
                {
                    _breakReminderService?.Snooze(5);
                }
                else if (arguments.Contains("dismiss"))
                {
                    _breakReminderService?.Dismiss();
                }
                else
                {
                    // Default click - open app with overlay
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                    Topmost = true;
                    Topmost = false;
                    ShowBreakOverlay();
                }
            });
        }

        private void Toast_Dismissed(ToastNotification sender, ToastDismissedEventArgs args)
        {
            // Only auto-dismiss if user swiped away (not if they clicked a button)
            if (args.Reason == ToastDismissalReason.UserCanceled && _breakNotificationPending)
            {
                Dispatcher.Invoke(() =>
                {
                    _breakNotificationPending = false;
                    _breakReminderService?.Dismiss();
                });
            }
        }

        private void ShowFallbackBalloon()
        {
            // Fallback to legacy balloon tip
            if (_trayIcon != null)
            {
                _trayIcon.BalloonTipTitle = "Time for a break!";
                _trayIcon.BalloonTipText = "Follow the 20-20-20 rule: Look away from screen for 20 seconds.";
                _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
                _trayIcon.ShowBalloonTip(10000);
            }
        }

        private void TrayIcon_BalloonTipClicked(object? sender, EventArgs e)
        {
            // Fallback balloon clicked
            if (_breakNotificationPending)
            {
                _breakNotificationPending = false;
                Show();
                WindowState = WindowState.Normal;
                Activate();
                Topmost = true;
                Topmost = false;
                ShowBreakOverlay();
            }
        }

        private void TrayIcon_BalloonTipClosed(object? sender, EventArgs e)
        {
            // Balloon closed (timeout or user clicked X)
            // DON'T dismiss - keep the notification pending
            // User must click the balloon or interact with overlay to dismiss
            // This prevents the timer from restarting when balloon auto-closes
        }

        private void ClearToastNotifications()
        {
            try
            {
                ToastNotificationManager.History.Clear(APP_ID);
            }
            catch
            {
                // Ignore errors
            }
        }

        private void OnBreakDismissed()
        {
            // Hide notification on UI thread
            Dispatcher.Invoke(() =>
            {
                BreakOverlay.Visibility = Visibility.Collapsed;
                _breakNotificationPending = false;
                ClearToastNotifications();
            });
        }

        private void OverlaySnoozeButton_Click(object sender, RoutedEventArgs e)
        {
            if (_breakReminderService != null)
            {
                bool snoozed = _breakReminderService.Snooze(5); // Snooze for 5 minutes
                if (!snoozed)
                {
                    // Max snoozes reached - service auto-dismissed
                    // Overlay will be hidden by OnBreakDismissed
                }
                else
                {
                    BreakOverlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        private void OverlayDismissButton_Click(object sender, RoutedEventArgs e)
        {
            _breakReminderService?.Dismiss();
        }

        private void BreakBackdrop_Click(object sender, MouseButtonEventArgs e)
        {
            // Clicking backdrop dismisses the overlay
            _breakReminderService?.Dismiss();
        }

        private void TurnOffLink_Click(object sender, MouseButtonEventArgs e)
        {
            // Permanently turn off break reminders
            if (_breakReminderService != null)
            {
                _breakReminderService.IsEnabled = false;
                _breakReminderService.Stop();
                _breakReminderService.SaveSettings();
                _breakReminderService.Dismiss();
            }
        }

        /// <summary>
        /// Show the break overlay and update snooze button state
        /// </summary>
        private void ShowBreakOverlay()
        {
            BreakOverlay.Visibility = Visibility.Visible;
            
            // Update snooze button based on remaining snoozes
            if (_breakReminderService != null)
            {
                int remaining = _breakReminderService.MaxSnoozeCount - _breakReminderService.SnoozeCount;
                if (remaining > 0)
                {
                    OverlaySnoozeButton.Content = $"Snooze 5 min ({remaining} left)";
                    OverlaySnoozeButton.IsEnabled = true;
                }
                else
                {
                    OverlaySnoozeButton.Content = "No snoozes left";
                    OverlaySnoozeButton.IsEnabled = false;
                }
            }
        }

        /// <summary>
        /// Update break reminder service with new settings (called from SettingsView)
        /// </summary>
        public void UpdateBreakReminderService()
        {
            if (_breakReminderService != null)
            {
                _breakReminderService.Stop();
                _breakReminderService.LoadSettings();
                _breakReminderService.Start();
            }
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
            
            // Handle balloon tip events for break reminders
            _trayIcon.BalloonTipClicked += TrayIcon_BalloonTipClicked;
            _trayIcon.BalloonTipClosed += TrayIcon_BalloonTipClosed;
        }

        private void ShowWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
            
            // If there's a pending break notification, show the overlay
            if (_breakReminderService?.IsBreakPending == true)
            {
                ShowBreakOverlay();
            }
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
            else if (WindowState == WindowState.Normal)
            {
                // Restore corner radius and margin
                WindowBorder.CornerRadius = new CornerRadius(8);
                WindowBorder.Margin = new Thickness(10);
                MaximizeIcon.Kind = PackIconKind.WindowMaximize;
                
                // If restored from minimized and there's a pending break, show overlay
                if (_breakReminderService?.IsBreakPending == true)
                {
                    ShowBreakOverlay();
                }
            }
            else
            {
                // Minimized - just update icon
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
            _breakReminderService?.Dispose();
            _focusSessionService?.Dispose();
            _windDownService?.Dispose();
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
                    MainContent.Opacity = 1;
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

        private void Focus_Click(object? sender, RoutedEventArgs e)
        {
            NavigateWithTransition(_focusView, NavFocus);
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
