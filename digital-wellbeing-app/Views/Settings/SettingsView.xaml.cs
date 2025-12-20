using System.Windows;
using System.Windows.Input;
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
