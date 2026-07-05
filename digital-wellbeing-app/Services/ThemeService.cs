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
        // Absolute path under the app's data folder. A relative "theme.json" landed in the
        // process CWD (unpredictable, and different between launch methods), so the saved
        // theme didn't reliably persist across restarts.
        private static readonly string FileName = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Pulse", "theme.json");
        private const string DarkThemeUri = "Styles/ThemeDark.xaml";
        private const string LightThemeUri = "Styles/ThemeLight.xaml";
        private const string PulseDarkUri = "Styles/Pulse.Dark.xaml";
        private const string PulseLightUri = "Styles/Pulse.Light.xaml";

        /// <summary>
        /// The section whose accent hue is currently promoted to the live Accent keys.
        /// Re-applied after a theme swap so the per-section retint survives.
        /// </summary>
        public static string CurrentSection { get; private set; } = "Dashboard";

        /// <summary>
        /// True while the dark palette is active. Drives per-section light/dark
        /// accent selection in <see cref="SetSection"/>. Defaults to dark (App.xaml default).
        /// </summary>
        public static bool IsDarkTheme { get; private set; } = true;

        public void Save(AppTheme mode)
        {
            // Serialize the enum as its NAME ("Dark"/"Light"/"Auto"); Load() reads it back
            // with GetString(). Writing the raw enum here emits a number, which GetString()
            // then rejects — so Load() always fell back to Dark and the toggle got stuck.
            var doc = JsonSerializer.Serialize(new { Mode = mode.ToString() });
            Directory.CreateDirectory(Path.GetDirectoryName(FileName)!);
            File.WriteAllText(FileName, doc);
        }

        public AppTheme Load()
        {
            if (!File.Exists(FileName)) return AppTheme.Dark;
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(FileName));
                if (json.RootElement.TryGetProperty("Mode", out var prop) &&
                    Enum.TryParse<AppTheme>(prop.GetString(), out var m))
                    return m;
            }
            catch { }
            return AppTheme.Dark;
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
            IsDarkTheme = isDark;

            // Swap the legacy palette (still used by any not-yet-migrated views)
            SwapPalette(isDark, DarkThemeUri, LightThemeUri, "ThemeDark", "ThemeLight");

            // Swap the Pulse palette (active design layer)
            SwapPalette(isDark, PulseDarkUri, PulseLightUri, "Pulse.Dark", "Pulse.Light");

            // A palette swap resets Accent to its default hue; re-promote the
            // active section's accent so the per-section retint survives.
            SetSection(CurrentSection);

            // Also update MaterialDesign's base theme for their components
            UpdateMaterialDesignTheme(isDark);

            // Update LiveCharts2 theme
            App.ConfigureLiveChartsTheme(isDark);
        }

        /// <summary>
        /// Replaces a palette ResourceDictionary at the end of the merged set so
        /// DynamicResource lookups pick up the new colours.
        /// </summary>
        private void SwapPalette(bool isDark, string darkUri, string lightUri,
                                 string darkMatch, string lightMatch)
        {
            var app = System.Windows.Application.Current;
            if (app == null) return;

            var dictionaries = app.Resources.MergedDictionaries;

            var existing = dictionaries.FirstOrDefault(d =>
                d.Source != null &&
                (d.Source.OriginalString.Contains(darkMatch) ||
                 d.Source.OriginalString.Contains(lightMatch)));

            if (existing != null)
            {
                dictionaries.Remove(existing);
            }

            dictionaries.Add(new ResourceDictionary
            {
                Source = new Uri(isDark ? darkUri : lightUri, UriKind.Relative)
            });
        }

        /// <summary>
        /// Promote a section's signature hue to the live Accent / Accent.Soft keys so
        /// the whole window retints to the active view (Pulse signature behaviour).
        /// section ∈ Dashboard, Screentime, Appusage, Sound, Focus, Weekly, Settings, Help.
        /// </summary>
        public static void SetSection(string section)
        {
            CurrentSection = section;
            var app = System.Windows.Application.Current;
            if (app == null) return;

            // Pulse uses a slightly deeper hue per section in light mode (better
            // contrast on white); fall back to the dark hue if no light variant exists.
            var accentKey = (!IsDarkTheme && app.Resources.Contains($"Accent.{section}.Light.Color"))
                ? $"Accent.{section}.Light.Color"
                : $"Accent.{section}.Color";
            if (app.Resources[accentKey] is not System.Windows.Media.Color color)
                return;

            app.Resources["Accent.Color"] = color;
            app.Resources["Accent"] = new System.Windows.Media.SolidColorBrush(color);
            app.Resources["Accent.Soft"] = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(0x26, color.R, color.G, color.B));
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
