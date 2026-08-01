using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using digital_wellbeing_app.Helpers;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;
using Button = System.Windows.Controls.Button;
using ToggleButton = System.Windows.Controls.Primitives.ToggleButton;
using ImageSource = System.Windows.Media.ImageSource;

namespace digital_wellbeing_app.Views.Limits
{
    /// <summary>App the user can pick from the "choose an app" list.</summary>
    public class AppLimitPickerDisplay
    {
        public string AppIdentifier { get; set; } = string.Empty;
        public string AppName { get; set; } = string.Empty;

        /// <summary>Raw process name (pre-<see cref="AppNameService.GetDisplayName"/>), persisted
        /// into <see cref="AppLimit.AppName"/> so it can be re-prettified correctly wherever it's
        /// displayed later - never persist the already-prettified <see cref="AppName"/> itself.</summary>
        public string RawAppName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public ImageSource? Icon { get; set; }
        public bool HasLimit { get; set; }
    }

    /// <summary>A configured AppLimit row, for the "active limits" list.</summary>
    public class AppLimitRowDisplay
    {
        public AppLimit Limit { get; set; } = null!;
        public string AppName { get; set; } = string.Empty;
        public ImageSource? Icon { get; set; }
        public string SummaryText { get; set; } = string.Empty;
    }

    public partial class AppLimitsView : System.Windows.Controls.UserControl
    {
        private readonly ObservableCollection<AppLimitPickerDisplay> _appPickerList = new();
        private readonly ObservableCollection<AppLimitRowDisplay> _activeLimitsList = new();
        private AppLimitPickerDisplay? _selectedApp;
        private FocusEnforcementLevel _selectedEnforcement = FocusEnforcementLevel.Warn;
        private bool _editingLimitEnabled = true;
        private bool _isChangingPickerSelection;
        private bool _loaded;

        public AppLimitsView()
        {
            InitializeComponent();
            PulseLayout.CapCenter(PageScroll, PageRoot);

            AppPickerCombo.ItemsSource = _appPickerList;
            ActiveLimitsList.ItemsSource = _activeLimitsList;
            PopulateTimeComboBoxes();

            Loaded += AppLimitsView_Loaded;
        }

        private void AppLimitsView_Loaded(object sender, RoutedEventArgs e)
        {
            LoadAppPicker();
            LoadActiveLimits();
            if (!_loaded)
            {
                ResetEditor();
                _loaded = true;
            }
        }

        #region Loading lists

        private void LoadAppPicker()
        {
            _appPickerList.Clear();

            try
            {
                var allApps = new List<AppUsageSession>();
                for (int i = 0; i < 7; i++)
                {
                    var date = DateTime.Today.AddDays(-i);
                    allApps.AddRange(DatabaseService.GetAppUsageSessionsForDate(date));
                }

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
                    var key = AppIdentity.NormalizeKey(app.ExecutablePath, app.AppName);
                    if (key.Length == 0) continue;

                    _appPickerList.Add(new AppLimitPickerDisplay
                    {
                        AppIdentifier = key,
                        AppName = AppNameService.GetDisplayName(app.AppName, app.ExecutablePath),
                        RawAppName = app.AppName,
                        ExecutablePath = app.ExecutablePath,
                        Icon = iconService.GetIconForApp(app.ExecutablePath),
                        HasLimit = DatabaseService.GetAppLimit(key) != null
                    });
                }

                NoAppsText.Visibility = _appPickerList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                AppPickerCombo.IsEnabled = _appPickerList.Count > 0;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Limits] Error loading app picker: {ex.Message}");
            }
        }

        private void LoadActiveLimits()
        {
            _activeLimitsList.Clear();

            try
            {
                var iconService = new AppIconService();
                var limits = DatabaseService.GetAllAppLimits()
                    .OrderBy(l => l.AppName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var limit in limits)
                {
                    _activeLimitsList.Add(new AppLimitRowDisplay
                    {
                        Limit = limit,
                        AppName = AppNameService.GetDisplayName(limit.AppName, limit.ExecutablePath),
                        Icon = iconService.GetIconForApp(limit.ExecutablePath),
                        SummaryText = BuildSummary(limit)
                    });
                }

                NoLimitsPanel.Visibility = _activeLimitsList.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Limits] Error loading active limits: {ex.Message}");
            }
        }

        private static string BuildSummary(AppLimit limit)
        {
            var parts = new List<string>();
            if (limit.DailyLimitMinutes > 0)
                parts.Add(limit.DailyLimitMinutes % 60 == 0
                    ? $"{limit.DailyLimitMinutes / 60} hr/day"
                    : $"{limit.DailyLimitMinutes} min/day");
            if (limit.ScheduleEnabled)
                parts.Add($"Unavailable {FormatTimeDisplay(limit.ScheduleStartHour, limit.ScheduleStartMinute)}–{FormatTimeDisplay(limit.ScheduleEndHour, limit.ScheduleEndMinute)}");

            parts.Add(limit.EnforcementLevel == FocusEnforcementLevel.Warn ? "Reminder" : "Minimize app");
            var summary = string.Join(" · ", parts);
            return limit.IsEnabled ? summary : $"Paused · {summary}";
        }

        #endregion

        #region Editor

        private void AppPickerCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isChangingPickerSelection && AppPickerCombo.SelectedItem is AppLimitPickerDisplay app)
                SelectApp(app);
        }

        private void EditLimit_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: AppLimitRowDisplay row })
            {
                var app = _appPickerList.FirstOrDefault(x => x.AppIdentifier == row.Limit.AppIdentifier)
                          ?? new AppLimitPickerDisplay
                {
                    AppIdentifier = row.Limit.AppIdentifier,
                    AppName = row.AppName,
                    RawAppName = row.Limit.AppName,
                    ExecutablePath = row.Limit.ExecutablePath,
                    Icon = row.Icon,
                    HasLimit = true
                };

                if (!_appPickerList.Contains(app))
                    _appPickerList.Add(app);
                AppPickerCombo.SelectedItem = app;
            }
        }

        private void ActiveLimitToggle_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton { Tag: AppLimitRowDisplay row } toggle) return;

            row.Limit.IsEnabled = toggle.IsChecked == true;
            row.Limit.LastUpdated = DateTime.Now;
            DatabaseService.SaveAppLimit(row.Limit);
            NotifyServiceChanged();
            LoadActiveLimits();
            LoadAppPicker();
        }

        private void SelectApp(AppLimitPickerDisplay app)
        {
            _selectedApp = app;
            SaveLimitButton.IsEnabled = true;
            ValidationText.Visibility = Visibility.Collapsed;
            CancelEditButton.Visibility = Visibility.Visible;

            var existing = DatabaseService.GetAppLimit(app.AppIdentifier);
            if (existing != null)
            {
                EditorSectionLabel.Text = "EDIT LIMIT";
                EditorTitleText.Text = $"Edit {app.AppName}";
                EditorHelpText.Text = "Change the allowance, schedule, or what happens when time is up.";
                SaveLimitButton.Content = "Save changes";
                DailyLimitTextBox.Text = existing.DailyLimitMinutes.ToString();
                SetEnforcement(existing.EnforcementLevel);
                ScheduleToggle.IsChecked = existing.ScheduleEnabled;
                SchedulePanel.Visibility = existing.ScheduleEnabled ? Visibility.Visible : Visibility.Collapsed;
                SelectTimeInComboBox(LimitStartTimeComboBox, existing.ScheduleStartHour, existing.ScheduleStartMinute);
                SelectTimeInComboBox(LimitEndTimeComboBox, existing.ScheduleEndHour, existing.ScheduleEndMinute);
                _editingLimitEnabled = existing.IsEnabled;
                RemoveLimitButton.Visibility = Visibility.Visible;
            }
            else
            {
                EditorSectionLabel.Text = "ADD A LIMIT";
                EditorTitleText.Text = $"Limit {app.AppName}";
                EditorHelpText.Text = "Choose a daily allowance and what Pulse should do when time is up.";
                SaveLimitButton.Content = "Add limit";
                DailyLimitTextBox.Text = "60";
                SetEnforcement(FocusEnforcementLevel.Warn);
                ScheduleToggle.IsChecked = false;
                SchedulePanel.Visibility = Visibility.Collapsed;
                SelectTimeInComboBox(LimitStartTimeComboBox, 9, 0);
                SelectTimeInComboBox(LimitEndTimeComboBox, 17, 0);
                _editingLimitEnabled = true;
                RemoveLimitButton.Visibility = Visibility.Collapsed;
            }
        }

        private void ResetEditor()
        {
            _selectedApp = null;
            _isChangingPickerSelection = true;
            AppPickerCombo.SelectedIndex = -1;
            _isChangingPickerSelection = false;
            EditorSectionLabel.Text = "ADD A LIMIT";
            EditorTitleText.Text = "Create an app limit";
            EditorHelpText.Text = "You can change or pause it at any time.";
            SaveLimitButton.Content = "Add limit";
            SaveLimitButton.IsEnabled = false;
            CancelEditButton.Visibility = Visibility.Collapsed;
            RemoveLimitButton.Visibility = Visibility.Collapsed;
            ValidationText.Visibility = Visibility.Collapsed;
            DailyLimitTextBox.Text = "60";
            SetEnforcement(FocusEnforcementLevel.Warn);
            ScheduleToggle.IsChecked = false;
            SchedulePanel.Visibility = Visibility.Collapsed;
            SelectTimeInComboBox(LimitStartTimeComboBox, 9, 0);
            SelectTimeInComboBox(LimitEndTimeComboBox, 17, 0);
            _editingLimitEnabled = true;
        }

        private void DailyPreset_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && int.TryParse(button.Tag?.ToString(), out var minutes))
                DailyLimitTextBox.Text = minutes.ToString();
        }

        private void CancelEditButton_Click(object sender, RoutedEventArgs e) => ResetEditor();

        private void LimitWarnOption_Click(object sender, RoutedEventArgs e) => SetEnforcement(FocusEnforcementLevel.Warn);
        private void LimitBlockOption_Click(object sender, RoutedEventArgs e) => SetEnforcement(FocusEnforcementLevel.Block);

        private void SetEnforcement(FocusEnforcementLevel level)
        {
            _selectedEnforcement = level;

            var accentBg = (System.Windows.Media.Brush)FindResource("Accent");
            var accentFg = (System.Windows.Media.Brush)FindResource("Accent.Ink");
            var card2 = (System.Windows.Media.Brush)FindResource("Card2");
            var ink = (System.Windows.Media.Brush)FindResource("Ink");

            LimitWarnBtn.Background = level == FocusEnforcementLevel.Warn ? accentBg : card2;
            LimitWarnBtn.Foreground = level == FocusEnforcementLevel.Warn ? accentFg : ink;
            LimitBlockBtn.Background = level == FocusEnforcementLevel.Block ? accentBg : card2;
            LimitBlockBtn.Foreground = level == FocusEnforcementLevel.Block ? accentFg : ink;
        }

        private void ScheduleToggle_Changed(object sender, RoutedEventArgs e)
        {
            SchedulePanel.Visibility = ScheduleToggle.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        }

        private void SaveLimitButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedApp == null) return;

            if (!int.TryParse(DailyLimitTextBox.Text, out int minutes) || minutes < 0)
            {
                ValidationText.Text = "Enter a valid number of minutes.";
                ValidationText.Visibility = Visibility.Visible;
                return;
            }
            minutes = Math.Min(minutes, 24 * 60);

            if (minutes == 0 && ScheduleToggle.IsChecked != true)
            {
                ValidationText.Text = "Choose a daily allowance, turn on unavailable hours, or use both.";
                ValidationText.Visibility = Visibility.Visible;
                return;
            }

            var (startHour, startMinute) = GetTimeFromComboBox(LimitStartTimeComboBox);
            var (endHour, endMinute) = GetTimeFromComboBox(LimitEndTimeComboBox);

            var limit = new AppLimit
            {
                AppIdentifier = _selectedApp.AppIdentifier,
                AppName = _selectedApp.RawAppName,
                ExecutablePath = _selectedApp.ExecutablePath,
                IsEnabled = _editingLimitEnabled,
                DailyLimitMinutes = minutes,
                ScheduleEnabled = ScheduleToggle.IsChecked == true,
                ScheduleStartHour = startHour,
                ScheduleStartMinute = startMinute,
                ScheduleEndHour = endHour,
                ScheduleEndMinute = endMinute,
                EnforcementLevel = _selectedEnforcement,
                LastUpdated = DateTime.Now
            };

            DatabaseService.SaveAppLimit(limit);
            NotifyServiceChanged();

            LoadAppPicker();
            LoadActiveLimits();
            ResetEditor();
        }

        private void RemoveLimitButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedApp == null) return;

            DatabaseService.DeleteAppLimit(_selectedApp.AppIdentifier);
            NotifyServiceChanged();

            LoadAppPicker();
            LoadActiveLimits();
            ResetEditor();
        }

        private static void NotifyServiceChanged()
        {
            (System.Windows.Application.Current as App)?.AppLimitSvc?.ReloadLimits();
        }

        #endregion

        #region Time combo boxes (mirrors SettingsView's Wind Down pattern)

        private void PopulateTimeComboBoxes()
        {
            LimitStartTimeComboBox.Items.Clear();
            LimitEndTimeComboBox.Items.Clear();

            for (int hour = 0; hour < 24; hour++)
            {
                for (int minute = 0; minute < 60; minute += 30)
                {
                    var timeStr = FormatTimeDisplay(hour, minute);
                    LimitStartTimeComboBox.Items.Add(new ComboBoxItem { Content = timeStr, Tag = $"{hour}:{minute}" });
                    LimitEndTimeComboBox.Items.Add(new ComboBoxItem { Content = timeStr, Tag = $"{hour}:{minute}" });
                }
            }
        }

        private static string FormatTimeDisplay(int hour, int minute)
        {
            var ampm = hour >= 12 ? "PM" : "AM";
            var displayHour = hour > 12 ? hour - 12 : (hour == 0 ? 12 : hour);
            return minute > 0 ? $"{displayHour}:{minute:D2} {ampm}" : $"{displayHour} {ampm}";
        }

        private static void SelectTimeInComboBox(System.Windows.Controls.ComboBox comboBox, int hour, int minute)
        {
            minute = minute >= 30 ? 30 : 0;
            var tagToFind = $"{hour}:{minute}";

            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Tag?.ToString() == tagToFind)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }

            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        private static (int hour, int minute) GetTimeFromComboBox(System.Windows.Controls.ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                var parts = tag.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int h) && int.TryParse(parts[1], out int m))
                    return (h, m);
            }
            return (9, 0);
        }

        #endregion
    }
}
