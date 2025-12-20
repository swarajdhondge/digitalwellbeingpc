using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.ViewModels;

namespace digital_wellbeing_app.Views.Settings
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        private readonly ThemeService _themeService = new();
        private readonly GoalService _goalService = new();
        private readonly SettingsService _settingsService = new();
        private bool _isLoadingGoal;
        private bool _isLoadingBreakReminder;
        private bool _isLoadingWindDown;

        public SettingsView()
        {
            InitializeComponent();
            
            // Load saved theme preference
            var savedTheme = _themeService.Load();
            switch (savedTheme)
            {
                case AppTheme.Light:
                    LightRadio.IsChecked = true;
                    UpdateThemeSelection("Light");
                    break;
                case AppTheme.Dark:
                    DarkRadio.IsChecked = true;
                    UpdateThemeSelection("Dark");
                    break;
                default: // Auto
                    AutoRadio.IsChecked = true;
                    UpdateThemeSelection("Auto");
                    break;
            }
            
            // Load startup preference
            StartupCheckBox.IsChecked = StartupService.IsEnabled();

            // Load goal settings
            LoadGoalSettings();

            // Load break reminder settings
            LoadBreakReminderSettings();

            // Load Wind Down settings
            LoadWindDownSettings();

            // Note: Hearing Protection threshold is disabled (Coming Soon)
            // Default is 75 dB, set in SettingsService.LoadHarmfulThreshold()
        }

        #region Goal Settings

        private void LoadGoalSettings()
        {
            _isLoadingGoal = true;
            
            var goal = _goalService.GetDailyScreenTimeGoal();
            GoalEnabledCheckBox.IsChecked = goal.HasValue;
            GoalInputPanel.Visibility = goal.HasValue ? Visibility.Visible : Visibility.Collapsed;

            if (goal.HasValue)
            {
                GoalHoursTextBox.Text = (goal.Value / 60).ToString();
                GoalMinutesTextBox.Text = (goal.Value % 60).ToString();
                UpdateCurrentGoalText(goal.Value);
            }
            else
            {
                GoalHoursTextBox.Text = "8";
                GoalMinutesTextBox.Text = "0";
                CurrentGoalText.Text = "No goal set";
            }

            _isLoadingGoal = false;
        }

        private void GoalEnabledCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingGoal) return;

            bool enabled = GoalEnabledCheckBox.IsChecked == true;
            GoalInputPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

            if (enabled)
            {
                SaveGoal();
            }
            else
            {
                _goalService.SetDailyScreenTimeGoal(null);
                CurrentGoalText.Text = "No goal set";
            }
        }

        private void GoalInput_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (_isLoadingGoal) return;
            if (GoalEnabledCheckBox.IsChecked != true) return;

            SaveGoal();
        }

        private void SaveGoal()
        {
            if (!int.TryParse(GoalHoursTextBox.Text, out int hours))
                hours = 0;
            if (!int.TryParse(GoalMinutesTextBox.Text, out int minutes))
                minutes = 0;

            // Clamp values
            hours = System.Math.Max(0, System.Math.Min(24, hours));
            minutes = System.Math.Max(0, System.Math.Min(59, minutes));

            int totalMinutes = (hours * 60) + minutes;

            if (totalMinutes > 0)
            {
                _goalService.SetDailyScreenTimeGoal(totalMinutes);
                UpdateCurrentGoalText(totalMinutes);
            }
            else
            {
                _goalService.SetDailyScreenTimeGoal(null);
                CurrentGoalText.Text = "Goal must be greater than 0";
            }
        }

        private void UpdateCurrentGoalText(int totalMinutes)
        {
            var hours = totalMinutes / 60;
            var mins = totalMinutes % 60;
            
            if (hours > 0 && mins > 0)
                CurrentGoalText.Text = $"Goal: {hours} hours {mins} minutes per day";
            else if (hours > 0)
                CurrentGoalText.Text = $"Goal: {hours} hours per day";
            else
                CurrentGoalText.Text = $"Goal: {mins} minutes per day";
        }

        #endregion

        #region Break Reminder Settings

        private void LoadBreakReminderSettings()
        {
            _isLoadingBreakReminder = true;

            bool enabled = _settingsService.LoadBreakReminderEnabled();
            int interval = _settingsService.LoadBreakReminderInterval();
            bool soundEnabled = _settingsService.LoadBreakReminderSound();

            BreakReminderToggle.IsChecked = enabled;
            BreakReminderOptionsPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            BreakReminderSoundPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            SoundToggle.IsChecked = soundEnabled;

            // Set interval selection
            foreach (System.Windows.Controls.ComboBoxItem item in IntervalComboBox.Items)
            {
                if (item.Tag?.ToString() == interval.ToString())
                {
                    IntervalComboBox.SelectedItem = item;
                    break;
                }
            }

            _isLoadingBreakReminder = false;
        }

        private void BreakReminderToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingBreakReminder) return;

            bool enabled = BreakReminderToggle.IsChecked == true;
            BreakReminderOptionsPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            BreakReminderSoundPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

            _settingsService.SaveBreakReminderEnabled(enabled);

            // Notify MainWindow to restart the service
            NotifyBreakReminderServiceChanged();
        }

        private void IntervalComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_isLoadingBreakReminder) return;

            var selected = IntervalComboBox.SelectedItem as System.Windows.Controls.ComboBoxItem;
            if (selected?.Tag != null && int.TryParse(selected.Tag.ToString(), out int interval))
            {
                _settingsService.SaveBreakReminderInterval(interval);
                NotifyBreakReminderServiceChanged();
            }
        }

        private void SoundToggle_Changed(object sender, RoutedEventArgs e)
        {
            if (_isLoadingBreakReminder) return;

            bool soundEnabled = SoundToggle.IsChecked == true;
            _settingsService.SaveBreakReminderSound(soundEnabled);
            NotifyBreakReminderServiceChanged();
        }

        private void NotifyBreakReminderServiceChanged()
        {
            // Find MainWindow and update break reminder service
            var mainWindow = Window.GetWindow(this) as MainWindow.MainWindow;
            mainWindow?.UpdateBreakReminderService();
        }

        #endregion

        #region Wind Down Settings

        private void LoadWindDownSettings()
        {
            _isLoadingWindDown = true;

            // Populate time combo boxes
            PopulateTimeComboBoxes();

            bool enabled = _settingsService.LoadWindDownEnabled();
            int startHour = _settingsService.LoadWindDownStartHour();
            int startMinute = _settingsService.LoadWindDownStartMinute();
            int endHour = _settingsService.LoadWindDownEndHour();
            int endMinute = _settingsService.LoadWindDownEndMinute();
            bool showNotification = _settingsService.LoadWindDownShowNotification();
            bool showVisualCue = _settingsService.LoadWindDownShowVisualCue();
            int visualStyle = _settingsService.LoadWindDownVisualStyle();

            WindDownToggle.IsChecked = enabled;
            WindDownOptionsPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;
            WindDownNotificationToggle.IsChecked = showNotification;
            WindDownVisualToggle.IsChecked = showVisualCue;
            VisualStylePanel.Visibility = showVisualCue ? Visibility.Visible : Visibility.Collapsed;

            // Select the saved times
            SelectTimeInComboBox(StartTimeComboBox, startHour, startMinute);
            SelectTimeInComboBox(EndTimeComboBox, endHour, endMinute);

            // Update visual style selection
            UpdateVisualStyleSelection((WindDownVisualStyle)visualStyle);

            // Update status text
            UpdateWindDownStatus(enabled, startHour, startMinute, endHour, endMinute);

            _isLoadingWindDown = false;
        }

        private void PopulateTimeComboBoxes()
        {
            StartTimeComboBox.Items.Clear();
            EndTimeComboBox.Items.Clear();

            // Generate time options in 30-minute increments
            for (int hour = 0; hour < 24; hour++)
            {
                for (int minute = 0; minute < 60; minute += 30)
                {
                    var timeStr = FormatTimeDisplay(hour, minute);
                    var startItem = new ComboBoxItem { Content = timeStr, Tag = $"{hour}:{minute}" };
                    var endItem = new ComboBoxItem { Content = timeStr, Tag = $"{hour}:{minute}" };
                    StartTimeComboBox.Items.Add(startItem);
                    EndTimeComboBox.Items.Add(endItem);
                }
            }
        }

        private string FormatTimeDisplay(int hour, int minute)
        {
            var ampm = hour >= 12 ? "PM" : "AM";
            var displayHour = hour > 12 ? hour - 12 : (hour == 0 ? 12 : hour);
            return minute > 0 ? $"{displayHour}:{minute:D2} {ampm}" : $"{displayHour} {ampm}";
        }

        private void SelectTimeInComboBox(System.Windows.Controls.ComboBox comboBox, int hour, int minute)
        {
            // Round minute to nearest 30
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

            // Default to first item if not found
            if (comboBox.Items.Count > 0)
                comboBox.SelectedIndex = 0;
        }

        private (int hour, int minute) GetTimeFromComboBox(System.Windows.Controls.ComboBox comboBox)
        {
            if (comboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                var parts = tag.Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out int hour) && int.TryParse(parts[1], out int minute))
                {
                    return (hour, minute);
                }
            }
            return (0, 0);
        }

        private void WindDownToggle_Changed(object sender, RoutedEventArgs e)
        {
            // Guard against events firing during XAML initialization
            if (_isLoadingWindDown || !IsLoaded || WindDownOptionsPanel == null) return;

            bool enabled = WindDownToggle.IsChecked == true;
            WindDownOptionsPanel.Visibility = enabled ? Visibility.Visible : Visibility.Collapsed;

            _settingsService.SaveWindDownEnabled(enabled);

            // When enabling Wind Down, also save the current time selections
            // to ensure the service has the correct schedule
            var (startHour, startMinute) = GetTimeFromComboBox(StartTimeComboBox);
            var (endHour, endMinute) = GetTimeFromComboBox(EndTimeComboBox);
            
            if (enabled)
            {
                // Save times when enabling to ensure they're persisted
                _settingsService.SaveWindDownStartTime(startHour, startMinute);
                _settingsService.SaveWindDownEndTime(endHour, endMinute);
            }
            
            UpdateWindDownStatus(enabled, startHour, startMinute, endHour, endMinute);

            NotifyWindDownServiceChanged();
        }

        private void StartTimeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingWindDown) return;

            var (hour, minute) = GetTimeFromComboBox(StartTimeComboBox);
            _settingsService.SaveWindDownStartTime(hour, minute);

            var (endHour, endMinute) = GetTimeFromComboBox(EndTimeComboBox);
            UpdateWindDownStatus(WindDownToggle.IsChecked == true, hour, minute, endHour, endMinute);

            NotifyWindDownServiceChanged();
        }

        private void EndTimeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoadingWindDown) return;

            var (hour, minute) = GetTimeFromComboBox(EndTimeComboBox);
            _settingsService.SaveWindDownEndTime(hour, minute);

            var (startHour, startMinute) = GetTimeFromComboBox(StartTimeComboBox);
            UpdateWindDownStatus(WindDownToggle.IsChecked == true, startHour, startMinute, hour, minute);

            NotifyWindDownServiceChanged();
        }

        private void WindDownNotificationToggle_Changed(object sender, RoutedEventArgs e)
        {
            // Guard against events firing during XAML initialization
            if (_isLoadingWindDown || !IsLoaded) return;

            bool showNotification = WindDownNotificationToggle.IsChecked == true;
            _settingsService.SaveWindDownShowNotification(showNotification);
            NotifyWindDownServiceChanged();
        }

        private void WindDownVisualToggle_Changed(object sender, RoutedEventArgs e)
        {
            // Guard against events firing during XAML initialization
            if (_isLoadingWindDown || !IsLoaded || VisualStylePanel == null) return;

            bool showVisual = WindDownVisualToggle.IsChecked == true;
            VisualStylePanel.Visibility = showVisual ? Visibility.Visible : Visibility.Collapsed;
            _settingsService.SaveWindDownShowVisualCue(showVisual);
            NotifyWindDownServiceChanged();
        }

        private void AmberStyle_Click(object sender, MouseButtonEventArgs e)
        {
            UpdateVisualStyleSelection(WindDownVisualStyle.Amber);
            _settingsService.SaveWindDownVisualStyle((int)WindDownVisualStyle.Amber);
            NotifyWindDownServiceChanged();
        }

        private void PurpleStyle_Click(object sender, MouseButtonEventArgs e)
        {
            UpdateVisualStyleSelection(WindDownVisualStyle.Purple);
            _settingsService.SaveWindDownVisualStyle((int)WindDownVisualStyle.Purple);
            NotifyWindDownServiceChanged();
        }

        private void DimStyle_Click(object sender, MouseButtonEventArgs e)
        {
            UpdateVisualStyleSelection(WindDownVisualStyle.Dim);
            _settingsService.SaveWindDownVisualStyle((int)WindDownVisualStyle.Dim);
            NotifyWindDownServiceChanged();
        }

        private void UpdateVisualStyleSelection(WindDownVisualStyle selected)
        {
            var defaultBrush = (System.Windows.Media.Brush)FindResource("Bg.Elevated");
            var accentBrush = (System.Windows.Media.Brush)FindResource("Accent.Primary");

            AmberStyleOption.Background = defaultBrush;
            PurpleStyleOption.Background = defaultBrush;
            DimStyleOption.Background = defaultBrush;

            switch (selected)
            {
                case WindDownVisualStyle.Amber:
                    AmberStyleOption.Background = accentBrush;
                    break;
                case WindDownVisualStyle.Purple:
                    PurpleStyleOption.Background = accentBrush;
                    break;
                case WindDownVisualStyle.Dim:
                    DimStyleOption.Background = accentBrush;
                    break;
            }
        }

        private void UpdateWindDownStatus(bool enabled, int startHour, int startMinute, int endHour, int endMinute)
        {
            if (!enabled)
            {
                WindDownStatusText.Text = "Wind Down is disabled";
                WindDownStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.MoonWaningCrescent;
                return;
            }

            var startTimeStr = FormatTimeDisplay(startHour, startMinute);
            var endTimeStr = FormatTimeDisplay(endHour, endMinute);
            WindDownStatusText.Text = $"Active from {startTimeStr} to {endTimeStr}";
            WindDownStatusIcon.Kind = MaterialDesignThemes.Wpf.PackIconKind.WeatherNight;
        }

        private void NotifyWindDownServiceChanged()
        {
            // Find MainWindow and update Wind Down service
            var mainWindow = Window.GetWindow(this) as MainWindow.MainWindow;
            mainWindow?.UpdateWindDownService();
        }

        #endregion

        #region Theme Settings

        private void UpdateThemeSelection(string selectedTheme)
        {
            // Reset all to default style
            var defaultBrush = (System.Windows.Media.Brush)FindResource("Bg.Elevated");
            LightThemeOption.Background = defaultBrush;
            DarkThemeOption.Background = defaultBrush;
            AutoThemeOption.Background = defaultBrush;

            // Highlight selected
            var accentBrush = (System.Windows.Media.Brush)FindResource("Accent.Primary");
            switch (selectedTheme)
            {
                case "Light":
                    LightThemeOption.Background = accentBrush;
                    break;
                case "Dark":
                    DarkThemeOption.Background = accentBrush;
                    break;
                case "Auto":
                    AutoThemeOption.Background = accentBrush;
                    break;
            }
        }

        private void LightTheme_Click(object sender, MouseButtonEventArgs e)
        {
            LightRadio.IsChecked = true;
            UpdateThemeSelection("Light");
            ApplyTheme(AppTheme.Light);
        }

        private void DarkTheme_Click(object sender, MouseButtonEventArgs e)
        {
            DarkRadio.IsChecked = true;
            UpdateThemeSelection("Dark");
            ApplyTheme(AppTheme.Dark);
        }

        private void AutoTheme_Click(object sender, MouseButtonEventArgs e)
        {
            AutoRadio.IsChecked = true;
            UpdateThemeSelection("Auto");
            ApplyTheme(AppTheme.Auto);
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            // This is called during initialization, skip if not loaded yet
            if (!IsLoaded) return;
            
            if (LightRadio.IsChecked == true)
                ApplyTheme(AppTheme.Light);
            else if (DarkRadio.IsChecked == true)
                ApplyTheme(AppTheme.Dark);
            else
                ApplyTheme(AppTheme.Auto);
        }

        private void ApplyTheme(AppTheme theme)
        {
            // Save and apply using ThemeService
            _themeService.Save(theme);
            _themeService.ApplyTheme(theme);
        }

        #endregion

        #region Startup Settings

        private void StartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            bool enable = (StartupCheckBox.IsChecked == true);
            StartupService.Enable(enable);
        }

        #endregion
    }
}
