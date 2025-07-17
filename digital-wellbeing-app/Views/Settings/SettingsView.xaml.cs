// File: Views/Settings/SettingsView.xaml.cs
using MaterialDesignThemes.Wpf;

namespace digital_wellbeing_app.Views.Settings
{
    public partial class SettingsView : System.Windows.Controls.UserControl
    {
        public SettingsView()
        {
            InitializeComponent();
            // seed the radio to whatever you like; 
            // for now we'll default to "Auto"
            AutoRadio.IsChecked = true;
        }

        private void ThemeRadio_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            // grab the global MaterialDesign theme
            var paletteHelper = new MaterialDesignThemes.Wpf.PaletteHelper();
            var theme = paletteHelper.GetTheme();

            if (LightRadio.IsChecked == true)
            {
                theme.SetBaseTheme(MaterialDesignThemes.Wpf.BaseTheme.Light);
            }
            else if (DarkRadio.IsChecked == true)
            {
                theme.SetBaseTheme(MaterialDesignThemes.Wpf.BaseTheme.Dark);
            }
            else // Auto
            {
                bool isDark = IsWindowsInDarkMode();
                theme.SetBaseTheme(isDark
                    ? MaterialDesignThemes.Wpf.BaseTheme.Dark
                    : MaterialDesignThemes.Wpf.BaseTheme.Light);
            }

            // apply it
            paletteHelper.SetTheme(theme);
        }

        /// <summary>
        /// Reads HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme.
        /// 0 = dark, 1 = light.
        /// </summary>
        private bool IsWindowsInDarkMode()
        {
            try
            {
                using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                    writable: false);
                if (key?.GetValue("AppsUseLightTheme") is int val)
                    return val == 0;
            }
            catch { }
            // fallback to light
            return false;
        }

        // (StartupCheckBox handlers left untouched)
        private void StartupCheckBox_Checked(object sender, System.Windows.RoutedEventArgs e)
        {
            bool enable = (StartupCheckBox.IsChecked == true);
            Services.StartupService.Enable(enable);
        }
    }
}
