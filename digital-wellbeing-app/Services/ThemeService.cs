using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;
using MaterialDesignThemes.Wpf;
using digital_wellbeing_app.ViewModels;  // brings in AppTheme

namespace digital_wellbeing_app.Services
{
    public class ThemeService
    {
        private const string FileName = "theme.json";
        private const string DarkThemeUri = "Styles/ThemeDark.xaml";
        private const string LightThemeUri = "Styles/ThemeLight.xaml";

        public void Save(AppTheme mode)
        {
            var doc = JsonSerializer.Serialize(new { Mode = mode });
            File.WriteAllText(FileName, doc);
        }

        public AppTheme Load()
        {
            if (!File.Exists(FileName)) return AppTheme.Auto;
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(FileName));
                if (json.RootElement.TryGetProperty("Mode", out var prop) &&
                    Enum.TryParse<AppTheme>(prop.GetString(), out var m))
                    return m;
            }
            catch { }
            return AppTheme.Auto;
        }

        public bool IsSystemInDarkMode()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
                return (int?)key?.GetValue("AppsUseLightTheme") == 0;
            }
            catch { return false; }
        }

        /// <summary>
        /// Apply the theme by swapping the palette dictionary and updating MaterialDesign
        /// </summary>
        public void ApplyTheme(AppTheme mode)
        {
            bool isDark = mode switch
            {
                AppTheme.Light => false,
                AppTheme.Dark => true,
                _ => IsSystemInDarkMode() // Auto
            };

            // Swap our custom palette dictionary
            SwapThemeDictionary(isDark);

            // Also update MaterialDesign's base theme for their components
            UpdateMaterialDesignTheme(isDark);
        }

        private void SwapThemeDictionary(bool isDark)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            var dictionaries = app.Resources.MergedDictionaries;
            
            // Find and remove existing theme palette
            var existingPalette = dictionaries.FirstOrDefault(d =>
                d.Source != null && 
                (d.Source.OriginalString.Contains("ThemeDark") || 
                 d.Source.OriginalString.Contains("ThemeLight")));

            if (existingPalette != null)
            {
                dictionaries.Remove(existingPalette);
            }

            // Add the correct palette at the END
            // DynamicResource bindings will pick up the new values
            var newPaletteUri = isDark ? DarkThemeUri : LightThemeUri;
            var newPalette = new ResourceDictionary
            {
                Source = new Uri(newPaletteUri, UriKind.Relative)
            };
            
            // Add at the end - DynamicResource uses the last definition found
            dictionaries.Add(newPalette);
        }

        private void UpdateMaterialDesignTheme(bool isDark)
        {
            try
            {
                var paletteHelper = new PaletteHelper();
                var theme = paletteHelper.GetTheme();
                
                // Set MaterialDesign base theme
                theme.SetBaseTheme(isDark ? BaseTheme.Dark : BaseTheme.Light);
                
                // Update primary/secondary to match our teal accent
                var tealPrimary = isDark 
                    ? (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#2DD4BF")
                    : (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#14B8A6");
                    
                theme.SetPrimaryColor(tealPrimary);
                theme.SetSecondaryColor(tealPrimary);
                
                paletteHelper.SetTheme(theme);
            }
            catch
            {
                // Ignore MaterialDesign errors - our custom theme still works
            }
        }
    }
}
