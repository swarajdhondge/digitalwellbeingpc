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
using digital_wellbeing_app.Views.Reports;
using digital_wellbeing_app.Views.Screen;
using digital_wellbeing_app.Views.Help;
using digital_wellbeing_app.Views.Settings;
using digital_wellbeing_app.Views.Sound;
using digital_wellbeing_app.Views.Welcome;
using MaterialDesignThemes.Wpf;
using Microsoft.Win32;
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
        private readonly WeeklyReportView _reportsView = new();
        private readonly HelpView _helpView = new();
        private readonly SettingsView _settingsView = new();

        // Break reminder service
        private Services.BreakReminderService? _breakReminderService;

        // Focus session service
        private Services.FocusSessionService? _focusSessionService;
        private string _currentDistractingApp = string.Empty;

        // Wind Down service
        private Services.WindDownService? _windDownService;

        // Goal notification tracking
        private bool _goalNotificationShownToday;
        private string _goalNotificationDate = string.Empty;
        private System.Windows.Threading.DispatcherTimer? _goalCheckTimer;

        public MainWindow()
        {
            InitializeComponent();

            // Set version dynamically from assembly PE file version info
            try
            {
                var location = System.Reflection.Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(location))
                {
                    var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(location);
                    SidebarVersionText.Text = $"v{fvi.ProductVersion ?? "1.7.0"}";
                }
            }
            catch { /* Keep default text from XAML */ }

            InitTrayIcon();
            InitBreakReminder();
            InitFocusSession();
            InitWindDown();
            InitGoalNotification();

            // Show welcome screen on first run, otherwise show dashboard
            var firstRunSettings = new Services.SettingsService();
            if (!firstRunSettings.LoadFirstRunCompleted())
            {
                var welcomeView = new WelcomeView();
                welcomeView.Completed += () => Dashboard_Click(null, new RoutedEventArgs());
                MainContent.Content = welcomeView;
            }
            else
            {
                MainContent.Content = _dashboardView;
            }

            // Initialise Pulse shell state: active rail item, section accent, theme icon
            NavDashboard.IsChecked = true;
            Services.ThemeService.SetSection("Dashboard");
            RefreshThemeToggleIcon();

            // Restore saved window position/size
            RestoreWindowState();

            // Handle maximize state for proper corner radius
            StateChanged += MainWindow_StateChanged;

            // Subscribe to system events for pause/resume of services
            SystemEvents.SessionSwitch += OnSystemSessionSwitch;
            SystemEvents.PowerModeChanged += OnSystemPowerModeChanged;
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
                _trayIcon.BalloonTipClicked -= OnFocusWarningBalloonClicked;
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

            // Include daily summary in the notification
            string summary = "";
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app?.ScreenTracker != null)
                {
                    var activeTime = app.ScreenTracker.CurrentActiveTime;
                    summary = $"\nYou used your PC for {(int)activeTime.TotalHours}h {activeTime.Minutes}m today.";
                }
            }
            catch { /* ignore - show notification without summary */ }

            _trayIcon.BalloonTipTitle = "🌙 Time to Wind Down";
            _trayIcon.BalloonTipText = $"Quiet hours have started. Consider wrapping up and getting ready for rest.{summary}";
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
                // Pass false to preserve notification state - don't show notification again on settings change
                _windDownService.Start(resetNotification: false);

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

        #region Goal Notifications

        private void InitGoalNotification()
        {
            // Check screen time goal every 60 seconds
            _goalCheckTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(60)
            };
            _goalCheckTimer.Tick += OnGoalCheckTick;
            _goalCheckTimer.Start();
        }

        private void OnGoalCheckTick(object? sender, EventArgs e)
        {
            try
            {
                var today = DateTime.Now.ToString("yyyy-MM-dd");

                // Reset flag on new day
                if (_goalNotificationDate != today)
                {
                    _goalNotificationShownToday = false;
                    _goalNotificationDate = today;
                }

                if (_goalNotificationShownToday) return;

                var app = System.Windows.Application.Current as App;
                if (app?.ScreenTracker == null) return;

                var goalService = new Services.GoalService();
                var goal = goalService.GetDailyScreenTimeGoal();
                if (goal == null) return;

                var currentTime = app.ScreenTracker.CurrentActiveTime;
                if (goalService.IsOverGoal(currentTime))
                {
                    _goalNotificationShownToday = true;
                    ShowGoalReachedNotification(goal.Value, currentTime);
                }
            }
            catch (Exception ex)
            {
                Services.LogService.Warning($"Goal check error: {ex.Message}");
            }
        }

        private void ShowGoalReachedNotification(int goalMinutes, TimeSpan currentTime)
        {
            if (_trayIcon == null) return;

            var goalFormatted = goalMinutes >= 60
                ? $"{goalMinutes / 60}h {goalMinutes % 60}m"
                : $"{goalMinutes}m";
            var currentFormatted = $"{(int)currentTime.TotalHours}h {currentTime.Minutes}m";

            _trayIcon.BalloonTipTitle = "Screen Time Goal Reached";
            _trayIcon.BalloonTipText = $"You've used your PC for {currentFormatted}, exceeding your {goalFormatted} goal. Consider taking a break.";
            _trayIcon.BalloonTipIcon = ToolTipIcon.Info;
            _trayIcon.ShowBalloonTip(5000);
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
                // Pass false to preserve timer state - don't reset on settings change
                _breakReminderService.Start(resetState: false);
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

            // Quick navigate
            _trayIcon.ContextMenuStrip.Items.Add("Open Dashboard", null, (s, e) => { ShowWindow(); Dashboard_Click(null, new RoutedEventArgs()); });
            _trayIcon.ContextMenuStrip.Items.Add("Focus", null, (s, e) => { ShowWindow(); Focus_Click(null, new RoutedEventArgs()); });
            _trayIcon.ContextMenuStrip.Items.Add("Reports", null, (s, e) => { ShowWindow(); Reports_Click(null, new RoutedEventArgs()); });
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

            // Focus session toggle
            var focusItem = new ToolStripMenuItem("Start Focus Session");
            focusItem.Click += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (_focusSessionService?.IsInFocusMode == true)
                    {
                        _focusSessionService.EndSession(false);
                    }
                    else
                    {
                        ShowWindow();
                        Focus_Click(null, new RoutedEventArgs());
                    }
                });
            };
            _trayIcon.ContextMenuStrip.Items.Add(focusItem);
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());

            // Settings and Updates
            _trayIcon.ContextMenuStrip.Items.Add("Settings", null, (s, e) => { ShowWindow(); Settings_Click(null, new RoutedEventArgs()); });
            _trayIcon.ContextMenuStrip.Items.Add("Check for Updates", null, async (s, e) =>
            {
                try
                {
                    var updateService = new Services.UpdateService();
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        await updateService.CheckAndPromptUpdateAsync();
                    });
                }
                catch (Exception ex)
                {
                    Services.LogService.Warning($"Manual tray update check failed: {ex.Message}");
                }
            });
            _trayIcon.ContextMenuStrip.Items.Add(new ToolStripSeparator());
            _trayIcon.ContextMenuStrip.Items.Add("Exit", null, (s, e) => Close());

            // Update focus item text dynamically when menu opens
            _trayIcon.ContextMenuStrip.Opening += (s, e) =>
            {
                focusItem.Text = _focusSessionService?.IsInFocusMode == true
                    ? "End Focus Session"
                    : "Start Focus Session";
            };
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

        private void ResizeGrip_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (WindowState == WindowState.Normal)
            {
                // Use native resize from bottom-right corner (WM_SYSCOMMAND + SC_SIZE + WMSZ_BOTTOMRIGHT)
                Platform.Windows.NativeMethods.SendMessage(
                    new WindowInteropHelper(this).Handle, 
                    0x112,      // WM_SYSCOMMAND
                    (IntPtr)0xF008,  // SC_SIZE + WMSZ_BOTTOMRIGHT
                    IntPtr.Zero);
            }
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
            // Save window position/size before closing
            SaveWindowState();

            // Unsubscribe from system events
            SystemEvents.SessionSwitch -= OnSystemSessionSwitch;
            SystemEvents.PowerModeChanged -= OnSystemPowerModeChanged;

            _goalCheckTimer?.Stop();
            _breakReminderService?.Dispose();
            _focusSessionService?.Dispose();
            _windDownService?.Dispose();
            _trayIcon?.Dispose();
        }

        /// <summary>
        /// Restore saved window position/size. Validates that the position is on a connected monitor.
        /// </summary>
        private void RestoreWindowState()
        {
            try
            {
                var settings = new Services.SettingsService();
                var state = settings.LoadWindowState();
                if (state == null) return;

                var (left, top, width, height, isMaximized) = state.Value;

                // Validate that the saved position is on a connected monitor
                var savedRect = new System.Drawing.Rectangle(
                    (int)left, (int)top, (int)width, (int)height);

                bool isOnScreen = false;
                foreach (var screen in System.Windows.Forms.Screen.AllScreens)
                {
                    if (screen.WorkingArea.IntersectsWith(savedRect))
                    {
                        isOnScreen = true;
                        break;
                    }
                }

                if (isOnScreen)
                {
                    Left = left;
                    Top = top;
                    Width = width;
                    Height = height;
                    WindowStartupLocation = WindowStartupLocation.Manual;
                }
                // else: keep CenterScreen default

                if (isMaximized)
                {
                    WindowState = WindowState.Maximized;
                }
            }
            catch
            {
                // If restore fails, just use default position/size
            }
        }

        /// <summary>
        /// Save current window position/size to settings.
        /// Uses RestoreBounds when maximized to preserve the normal-state dimensions.
        /// </summary>
        private void SaveWindowState()
        {
            try
            {
                var settings = new Services.SettingsService();
                bool isMaximized = WindowState == WindowState.Maximized;

                // When maximized, use RestoreBounds to get the "normal" position/size
                var bounds = isMaximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
                settings.SaveWindowState(bounds.Left, bounds.Top, bounds.Width, bounds.Height, isMaximized);
            }
            catch
            {
                // Ignore save errors
            }
        }

        /// <summary>
        /// Handle system session events (lock/unlock) - pause/resume services to prevent timer drift
        /// </summary>
        private void OnSystemSessionSwitch(object sender, SessionSwitchEventArgs e)
        {
            switch (e.Reason)
            {
                case SessionSwitchReason.SessionLock:
                case SessionSwitchReason.SessionLogoff:
                case SessionSwitchReason.ConsoleDisconnect:
                case SessionSwitchReason.RemoteDisconnect:
                    // Screen locked - stop timers to prevent drift/unexpected notifications
                    _breakReminderService?.Stop();
                    _windDownService?.Stop();
                    System.Diagnostics.Debug.WriteLine("[System] Session locked - services paused");
                    break;

                case SessionSwitchReason.SessionUnlock:
                case SessionSwitchReason.SessionLogon:
                case SessionSwitchReason.ConsoleConnect:
                case SessionSwitchReason.RemoteConnect:
                    // Screen unlocked - restart services (preserving state)
                    _breakReminderService?.Start(resetState: false);
                    _windDownService?.Start(resetNotification: false);
                    System.Diagnostics.Debug.WriteLine("[System] Session unlocked - services resumed");
                    break;
            }
        }

        /// <summary>
        /// Handle power mode changes (sleep/resume) - pause/resume services
        /// </summary>
        private void OnSystemPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            switch (e.Mode)
            {
                case PowerModes.Suspend:
                    // PC sleeping - stop timers
                    _breakReminderService?.Stop();
                    _windDownService?.Stop();
                    System.Diagnostics.Debug.WriteLine("[System] Power suspend - services paused");
                    break;

                case PowerModes.Resume:
                    // PC waking - restart services (preserving state)
                    _breakReminderService?.Start(resetState: false);
                    _windDownService?.Start(resetNotification: false);
                    System.Diagnostics.Debug.WriteLine("[System] Power resume - services resumed");
                    break;
            }
        }

        #endregion

        #region Navigation

        /// <summary>
        /// Switch the hosted view, retint the whole window to the section accent,
        /// and update the top-bar title/subtitle.
        /// </summary>
        private void NavigateTo(System.Windows.Controls.UserControl view,
                                System.Windows.Controls.Primitives.ToggleButton railItem,
                                string section, string title, string subtitle)
        {
            railItem.IsChecked = true;
            Services.ThemeService.SetSection(section);
            if (TitleText != null) TitleText.Text = title;
            if (SubtitleText != null) SubtitleText.Text = subtitle;
            NavigateWithTransition(view);
        }

        /// <summary>
        /// Navigate to a new view with a fade transition animation.
        /// </summary>
        private void NavigateWithTransition(object newContent)
        {
            // Skip if already on this page
            if (MainContent.Content == newContent) return;

            try
            {
                if (TryFindResource("PageFadeIn") is Storyboard fadeIn)
                {
                    MainContent.Opacity = 0;
                    MainContent.Content = newContent;
                    fadeIn.Begin(MainContent);
                }
                else
                {
                    MainContent.Content = newContent;
                    MainContent.Opacity = 1;
                }
            }
            catch
            {
                MainContent.Content = newContent;
                MainContent.Opacity = 1;
            }
        }

        private void Dashboard_Click(object? sender, RoutedEventArgs e)
            => NavigateTo(_dashboardView, NavDashboard, "Dashboard", "Today", "Your day at a glance");

        private void ScreenTime_Click(object? sender, RoutedEventArgs e)
            => NavigateTo(_screenView, NavScreen, "Screentime", "Screen time", "Time spent at your computer");

        private void Sound_Click(object? sender, RoutedEventArgs e)
            => NavigateTo(_soundView, NavSound, "Sound", "Hearing", "Protect your hearing");

        private void AppUsage_Click(object? sender, RoutedEventArgs e)
            => NavigateTo(_appUsageView, NavApps, "Appusage", "App usage", "Where your time goes");

        private void Focus_Click(object? sender, RoutedEventArgs e)
            => NavigateTo(_focusView, NavFocus, "Focus", "Focus", "Stay on task");

        private void Reports_Click(object? sender, RoutedEventArgs e)
            => NavigateTo(_reportsView, NavReports, "Weekly", "Insights", "Your weekly trends");

        private void Help_Click(object? sender, RoutedEventArgs e)
            => NavigateTo(_helpView, NavHelp, "Help", "Help", "Questions, answered");

        private void Settings_Click(object? sender, RoutedEventArgs e)
            => NavigateTo(_settingsView, NavSettings, "Settings", "Settings", "Preferences & privacy");

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Toggle between the light and dark Pulse palettes from the top bar.
        /// </summary>
        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            var ts = new Services.ThemeService();
            var next = IsEffectiveDark(ts) ? ViewModels.AppTheme.Light : ViewModels.AppTheme.Dark;
            ts.ApplyTheme(next);
            ts.Save(next);
            RefreshThemeToggleIcon();
        }

        private void RefreshThemeToggleIcon()
        {
            var ts = new Services.ThemeService();
            bool isDark = IsEffectiveDark(ts);
            ThemeToggleIcon.Kind = isDark ? PackIconKind.WeatherSunny : PackIconKind.WeatherNight;
            ThemeToggleButton.ToolTip = isDark ? "Switch to light theme" : "Switch to dark theme";
        }

        private static bool IsEffectiveDark(Services.ThemeService ts)
        {
            return ts.Load() switch
            {
                ViewModels.AppTheme.Light => false,
                ViewModels.AppTheme.Dark => true,
                _ => ts.IsSystemInDarkMode()
            };
        }

        /// <summary>
        /// Show the privacy/welcome screen (called from Settings "View privacy info" link).
        /// </summary>
        public void ShowPrivacyInfo()
        {
            var welcomeView = new WelcomeView();
            welcomeView.Completed += () => NavigateWithTransition(_settingsView);
            MainContent.Content = welcomeView;
        }

        #endregion
    }
}
