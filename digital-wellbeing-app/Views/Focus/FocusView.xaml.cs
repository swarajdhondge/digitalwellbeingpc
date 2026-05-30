using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using ImageSource = System.Windows.Media.ImageSource;
using Button = System.Windows.Controls.Button;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;

namespace digital_wellbeing_app.Views.Focus
{
    /// <summary>
    /// App display model for the category list
    /// </summary>
    public class AppCategoryDisplay : INotifyPropertyChanged
    {
        public string AppIdentifier { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public ImageSource? Icon { get; set; }
        
        private string _category = "Neutral";
        public string Category
        {
            get => _category;
            set
            {
                if (_category != value)
                {
                    _category = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Category)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// Session history display model
    /// </summary>
    public class SessionHistoryDisplay
    {
        public string DurationText { get; set; } = string.Empty;
        public string TimeText { get; set; } = string.Empty;
        public string DistractionsText { get; set; } = string.Empty;
        public string StatusIcon { get; set; } = "Check";
        public Brush StatusColor { get; set; } = Brushes.Green;
    }

    public partial class FocusView : System.Windows.Controls.UserControl
    {
        private FocusSessionService? _focusService;
        private bool _serviceInitialized;
        private int _selectedDuration = 25;
        private readonly ObservableCollection<AppCategoryDisplay> _appDisplayList = new();
        private readonly ObservableCollection<SessionHistoryDisplay> _sessionHistory = new();

        public FocusView()
        {
            InitializeComponent();

            AppCategoryList.ItemsSource = _appDisplayList;
            SessionHistoryList.ItemsSource = _sessionHistory;

            Loaded += FocusView_Loaded;
        }

        private void FocusView_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_serviceInitialized)
            {
                InitializeFocusService();
                _serviceInitialized = true;
            }
            else if (_focusService?.IsInFocusMode == true)
            {
                // Re-sync UI state when navigating back
                ShowActiveState();
            }

            LoadAppCategories();
            LoadSessionHistory();
            UpdateEnforcementUI();
            UpdateDurationSelection();
        }

        private void InitializeFocusService()
        {
            // Get service from MainWindow if available, or create new one
            var mainWindow = Window.GetWindow(this) as MainWindow.MainWindow;
            _focusService = mainWindow?.GetFocusSessionService();

            if (_focusService == null)
            {
                _focusService = new FocusSessionService();
            }

            // Subscribe to events (once only - guarded by _serviceInitialized)
            _focusService.SessionStarted += OnSessionStarted;
            _focusService.SessionEnded += OnSessionEnded;
            _focusService.SessionTick += OnSessionTick;

            // Update UI based on current state
            if (_focusService.IsInFocusMode)
            {
                ShowActiveState();
            }
        }

        #region Duration Selection

        private void UpdateDurationSelection()
        {
            // Get theme-aware Pulse brushes
            var secondaryBg = (Brush)FindResource("Card2");
            var primaryBg = (Brush)FindResource("Accent");
            var primaryFg = (Brush)FindResource("Accent.Ink");
            var secondaryFg = (Brush)FindResource("Ink");

            // Update each button's Background AND Foreground
            SetDurationButtonStyle(Duration15Btn, _selectedDuration == 15, primaryBg, primaryFg, secondaryBg, secondaryFg);
            SetDurationButtonStyle(Duration25Btn, _selectedDuration == 25, primaryBg, primaryFg, secondaryBg, secondaryFg);
            SetDurationButtonStyle(Duration45Btn, _selectedDuration == 45, primaryBg, primaryFg, secondaryBg, secondaryFg);
            SetDurationButtonStyle(Duration60Btn, _selectedDuration == 60, primaryBg, primaryFg, secondaryBg, secondaryFg);
            SetDurationButtonStyle(Duration90Btn, _selectedDuration == 90, primaryBg, primaryFg, secondaryBg, secondaryFg);
        }

        private void SetDurationButtonStyle(Button btn, bool isSelected, Brush primaryBg, Brush primaryFg, Brush secondaryBg, Brush secondaryFg)
        {
            btn.Background = isSelected ? primaryBg : secondaryBg;
            btn.Foreground = isSelected ? primaryFg : secondaryFg;
        }

        private void DurationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                if (int.TryParse(btn.Tag.ToString(), out int duration))
                {
                    _selectedDuration = duration;
                    CustomDurationPanel.Visibility = Visibility.Collapsed;
                    UpdateDurationSelection();
                }
            }
        }

        private void CustomDuration_Click(object sender, RoutedEventArgs e)
        {
            CustomDurationPanel.Visibility = Visibility.Visible;
            _selectedDuration = 0; // Custom
            UpdateDurationSelection();
        }

        #endregion

        #region Focus Session Control

        private void StartFocusButton_Click(object sender, RoutedEventArgs e)
        {
            if (_focusService == null) return;

            int duration = _selectedDuration;
            
            // If custom duration, parse from textbox
            if (duration == 0 && int.TryParse(CustomDurationTextBox.Text, out int customDuration))
            {
                duration = Math.Max(1, Math.Min(240, customDuration)); // Clamp 1-240 minutes
            }
            
            if (duration <= 0) duration = 25; // Default

            _focusService.StartSession(duration);
        }

        private void StopFocusButton_Click(object sender, RoutedEventArgs e)
        {
            if (_focusService == null) return;
            
            // Show custom confirmation dialog via MainWindow
            var mainWindow = Window.GetWindow(this) as MainWindow.MainWindow;
            mainWindow?.ShowEndSessionConfirmation();
        }

        private void OnSessionStarted()
        {
            Dispatcher.Invoke(() =>
            {
                ShowActiveState();
            });
        }

        private void OnSessionEnded(bool completed)
        {
            Dispatcher.Invoke(() =>
            {
                ShowInactiveState();
                LoadSessionHistory();
            });
        }

        private void OnSessionTick()
        {
            Dispatcher.Invoke(() =>
            {
                UpdateTimerDisplay();
            });
        }

        private void ShowActiveState()
        {
            FocusInactivePanel.Visibility = Visibility.Collapsed;
            FocusActivePanel.Visibility = Visibility.Visible;
            UpdateTimerDisplay();
        }

        private void ShowInactiveState()
        {
            FocusInactivePanel.Visibility = Visibility.Visible;
            FocusActivePanel.Visibility = Visibility.Collapsed;
        }

        private void UpdateTimerDisplay()
        {
            if (_focusService?.CurrentSession == null) return;

            var remaining = _focusService.TimeRemaining;
            TimerDisplay.Text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";

            // Update progress bar
            var progress = _focusService.Progress / 100.0;
            var containerWidth = ((Grid)ProgressBar.Parent).ActualWidth;
            ProgressBar.Width = containerWidth * progress;

            // Update stats
            DistractionsCount.Text = _focusService.CurrentSession.DistractionWarnings.ToString();
            EnforcementDisplay.Text = _focusService.EnforcementLevel.ToString();
            
            // Count entertainment apps
            var entertainmentCount = _focusService.GetAllAppCategories()
                .Count(x => x.Value == AppCategoryType.Entertainment);
            BlockedAppsCount.Text = entertainmentCount.ToString();
        }

        #endregion

        #region Enforcement Level

        private void UpdateEnforcementUI()
        {
            var level = _focusService?.EnforcementLevel ?? FocusEnforcementLevel.Warn;
            
            var defaultBg = (Brush)FindResource("Card2");
            var accentBg = (Brush)FindResource("Accent");
            var accentFg = (Brush)FindResource("Accent.Ink");
            var defaultFg = (Brush)FindResource("Ink");
            var secondaryFg = (Brush)FindResource("Ink2");

            // Update Warn option
            bool warnSelected = level == FocusEnforcementLevel.Warn;
            WarnOption.Background = warnSelected ? accentBg : defaultBg;
            WarnCheck.Visibility = warnSelected ? Visibility.Visible : Visibility.Collapsed;
            WarnCheck.Foreground = warnSelected ? accentFg : accentBg;
            WarnIcon.Foreground = warnSelected ? accentFg : secondaryFg;
            WarnTitle.Foreground = warnSelected ? accentFg : defaultFg;
            WarnDescription.Foreground = warnSelected ? accentFg : secondaryFg;
            WarnDescription.Opacity = warnSelected ? 0.8 : 1.0;

            // Update Block option  
            bool blockSelected = level == FocusEnforcementLevel.Block;
            BlockOption.Background = blockSelected ? accentBg : defaultBg;
            BlockCheck.Visibility = blockSelected ? Visibility.Visible : Visibility.Collapsed;
            BlockCheck.Foreground = blockSelected ? accentFg : accentBg;
            BlockIcon.Foreground = blockSelected ? accentFg : secondaryFg;
            BlockTitle.Foreground = blockSelected ? accentFg : defaultFg;
            BlockDescription.Foreground = blockSelected ? accentFg : secondaryFg;
            BlockDescription.Opacity = blockSelected ? 0.8 : 1.0;
        }

        private void WarnOption_Click(object sender, MouseButtonEventArgs e)
        {
            if (_focusService == null) return;
            _focusService.EnforcementLevel = FocusEnforcementLevel.Warn;
            _focusService.SaveSettings();
            UpdateEnforcementUI();
        }

        private void BlockOption_Click(object sender, MouseButtonEventArgs e)
        {
            if (_focusService == null) return;
            _focusService.EnforcementLevel = FocusEnforcementLevel.Block;
            _focusService.SaveSettings();
            UpdateEnforcementUI();
        }

        #endregion

        #region App Categories

        private void LoadAppCategories()
        {
            _appDisplayList.Clear();

            try
            {
                // Get apps from the last 7 days for better coverage
                var allApps = new List<AppUsageSession>();
                for (int i = 0; i < 7; i++)
                {
                    var date = DateTime.Today.AddDays(-i);
                    var dailyApps = DatabaseService.GetAppUsageSessionsForDate(date);
                    allApps.AddRange(dailyApps);
                }

                // Group by app name and calculate total usage, sorted by most used
                var recentApps = allApps
                    .Where(x => !string.IsNullOrEmpty(x.AppName))
                    .Where(x => !x.AppName.Equals("DigitalWellbeing", StringComparison.OrdinalIgnoreCase))
                    .Where(x => !x.AppName.Equals("digital-wellbeing-app", StringComparison.OrdinalIgnoreCase))
                    .GroupBy(x => new { x.AppName, x.ExecutablePath })
                    .Select(g => new
                    {
                        AppName = g.Key.AppName,
                        ExecutablePath = g.Key.ExecutablePath,
                        TotalUsage = TimeSpan.FromSeconds(g.Sum(s => s.Duration.TotalSeconds))
                    })
                    .OrderByDescending(x => x.TotalUsage)
                    .Take(25)
                    .ToList();

                var iconService = new AppIconService();

                foreach (var app in recentApps)
                {
                    var category = _focusService?.GetAppCategory(app.AppName) ?? AppCategoryType.Uncategorized;
                    
                    var display = new AppCategoryDisplay
                    {
                        AppIdentifier = app.AppName,
                        AppName = AppNameService.GetDisplayName(app.AppName, app.ExecutablePath),
                        ExecutablePath = app.ExecutablePath,
                        Category = CategoryToDisplayName(category),
                        Icon = iconService.GetIconForApp(app.ExecutablePath)
                    };

                    _appDisplayList.Add(display);
                }

                System.Diagnostics.Debug.WriteLine($"[Focus] Loaded {_appDisplayList.Count} apps for categorization");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Focus] Error loading apps: {ex.Message}");
            }
        }

        private void SetWorkCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppCategoryDisplay app)
            {
                SetCategory(app, AppCategoryType.Work);
            }
        }

        private void SetEntertainmentCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppCategoryDisplay app)
            {
                SetCategory(app, AppCategoryType.Entertainment);
            }
        }

        private void SetNeutralCategory_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is AppCategoryDisplay app)
            {
                SetCategory(app, AppCategoryType.Uncategorized); // Enum stays same, display is "Neutral"
            }
        }

        private void SetCategory(AppCategoryDisplay app, AppCategoryType category)
        {
            _focusService?.SetAppCategory(
                app.AppIdentifier,
                app.AppName,
                app.ExecutablePath,
                category);
            
            app.Category = CategoryToDisplayName(category);
        }

        /// <summary>
        /// Convert enum to display name (Uncategorized -> Neutral)
        /// </summary>
        private static string CategoryToDisplayName(AppCategoryType category)
        {
            return category switch
            {
                AppCategoryType.Work => "Work",
                AppCategoryType.Entertainment => "Entertainment",
                AppCategoryType.Uncategorized => "Neutral",
                _ => "Neutral"
            };
        }

        #endregion

        #region Session History

        private void LoadSessionHistory()
        {
            _sessionHistory.Clear();

            try
            {
                var sessions = DatabaseService.GetFocusSessionHistory(7);
                var todaySessions = sessions.Where(s => s.SessionDate == DateTime.Today.ToString("yyyy-MM-dd")).ToList();

                // Update today's stats
                var todayFocusTime = TimeSpan.FromMinutes(todaySessions.Sum(s => s.Duration.TotalMinutes));
                TodayFocusTime.Text = TimeFormatHelper.FormatCompact(todayFocusTime);
                TodaySessionCount.Text = todaySessions.Count(s => s.Completed).ToString();

                // Add history items
                foreach (var session in sessions.Take(10))
                {
                    var display = new SessionHistoryDisplay
                    {
                        DurationText = TimeFormatHelper.FormatCompact(session.Duration),
                        TimeText = session.StartTime.ToString("ddd, MMM d @ h:mm tt"),
                        DistractionsText = session.DistractionWarnings > 0 
                            ? $"{session.DistractionWarnings} distractions" 
                            : "No distractions",
                        StatusIcon = session.Completed ? "Check" : "Close",
                        StatusColor = session.Completed
                            ? (Brush)FindResource("Good")
                            : (Brush)FindResource("Danger")
                    };
                    _sessionHistory.Add(display);
                }

                NoHistoryText.Visibility = _sessionHistory.Count == 0 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
            catch
            {
                NoHistoryText.Visibility = Visibility.Visible;
            }
        }

        #endregion
    }
}

