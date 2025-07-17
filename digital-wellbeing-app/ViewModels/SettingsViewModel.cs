using System;
using System.ComponentModel;
using MaterialDesignThemes.Wpf;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.ViewModels
{
    public enum AppTheme { Light, Dark, Auto }

    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly PaletteHelper _palette = new();
        private readonly ThemeService _svc = new();

        private bool _isLight, _isDark, _isAuto;
        public bool IsLight
        {
            get => _isLight;
            set { if (_isLight == value) return; _isLight = value; OnChanged(); Apply(); }
        }
        public bool IsDark
        {
            get => _isDark;
            set { if (_isDark == value) return; _isDark = value; OnChanged(); Apply(); }
        }
        public bool IsAuto
        {
            get => _isAuto;
            set { if (_isAuto == value) return; _isAuto = value; OnChanged(); Apply(); }
        }

        public SettingsViewModel()
        {
            var mode = _svc.Load();
            _isLight = mode == AppTheme.Light;
            _isDark = mode == AppTheme.Dark;
            _isAuto = mode == AppTheme.Auto;
        }

        private void Apply()
        {
            var themeMode = IsLight ? AppTheme.Light
                          : IsDark ? AppTheme.Dark
                                    : AppTheme.Auto;

            _svc.Save(themeMode);

            var theme = _palette.GetTheme();
            bool dark = themeMode switch
            {
                AppTheme.Light => false,
                AppTheme.Dark => true,
                _ => _svc.IsSystemInDarkMode()
            };
            theme.SetBaseTheme(dark ? BaseTheme.Dark : BaseTheme.Light);
            _palette.SetTheme(theme);
        }

        private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string n = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
