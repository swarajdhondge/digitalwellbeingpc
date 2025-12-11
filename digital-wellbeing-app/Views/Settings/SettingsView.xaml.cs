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
        private bool _isLoadingGoal;

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
