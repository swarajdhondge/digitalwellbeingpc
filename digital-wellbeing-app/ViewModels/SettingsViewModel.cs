using System.ComponentModel;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.ViewModels
{
    public enum AppTheme { Light, Dark, Auto }

    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly ThemeService _themeService = new();

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
            var mode = _themeService.Load();
            _isLight = mode == AppTheme.Light;
            _isDark = mode == AppTheme.Dark;
            _isAuto = mode == AppTheme.Auto;
        }

        private void Apply()
        {
            var themeMode = IsLight ? AppTheme.Light
                          : IsDark ? AppTheme.Dark
                                   : AppTheme.Auto;

            // Save preference
            _themeService.Save(themeMode);
            
            // Apply theme (swaps palette dictionary + updates MaterialDesign)
            _themeService.ApplyTheme(themeMode);
        }

        private void OnChanged([System.Runtime.CompilerServices.CallerMemberName] string n = "")
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
