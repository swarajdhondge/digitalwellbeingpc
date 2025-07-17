using System;
using System.IO;
using System.Text.Json;
using Microsoft.Win32;
using digital_wellbeing_app.ViewModels;  // brings in AppTheme

namespace digital_wellbeing_app.Services
{
    public class ThemeService
    {
        // Single filename, single class—no duplicates:
        private const string FileName = "theme.json";

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
    }
}
