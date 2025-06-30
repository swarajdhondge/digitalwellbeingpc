using System.Windows;
using System.Windows.Controls;
using MaterialDesignThemes.Wpf;
using digital_wellbeing_app.Services;
using digital_wellbeing_app.ViewModels;

namespace digital_wellbeing_app.Views.Settings
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            InitializeComponent();

            // Initialize the radio buttons to the saved theme
            var svc = new ThemeService();
            var mode = svc.Load();
            LightRadio.IsChecked = mode == AppTheme.Light;
            DarkRadio.IsChecked = mode == AppTheme.Dark;
            AutoRadio.IsChecked = mode == AppTheme.Auto;
        }

        private void ThemeRadio_Checked(object sender, RoutedEventArgs e)
        {
            var svc = new ThemeService();
            var picker = new PaletteHelper();

            // Determine which is selected
            bool isDark;
            if (LightRadio.IsChecked == true)
            {
                svc.Save(AppTheme.Light);
                isDark = false;
            }
            else if (DarkRadio.IsChecked == true)
            {
                svc.Save(AppTheme.Dark);
                isDark = true;
            }
            else
            {
                svc.Save(AppTheme.Auto);
                isDark = svc.IsSystemInDarkMode();
            }

            // Apply immediately
            var theme = picker.GetTheme();
            theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
            picker.SetTheme(theme);
        }
    }
}
