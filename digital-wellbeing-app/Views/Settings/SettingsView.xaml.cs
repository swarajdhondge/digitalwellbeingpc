using System.Windows;
using System.Windows.Input;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.ViewModels;

namespace digital_wellbeing_app.Views.Settings
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        private readonly ThemeService _themeService = new();

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
        }

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

        private void StartupCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            bool enable = (StartupCheckBox.IsChecked == true);
            StartupService.Enable(enable);
        }
    }
}
