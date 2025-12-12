namespace digital_wellbeing_app.Services
{
    public class SettingsService
    {
        private const string FolderName = "Digital Wellbeing";
        private const string FileName = "settings.json";
        private readonly string _path;
        private readonly System.Collections.Generic.Dictionary<string, object> _values;

        public SettingsService()
        {
            var folder = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                FolderName);
            System.IO.Directory.CreateDirectory(folder);

            _path = System.IO.Path.Combine(folder, FileName);
            if (System.IO.File.Exists(_path))
            {
                try
                {
                    var json = System.IO.File.ReadAllText(_path);
                    _values = System.Text.Json.JsonSerializer
                        .Deserialize<System.Collections.Generic.Dictionary<string, object>>(json)
                        ?? new System.Collections.Generic.Dictionary<string, object>();
                }
                catch
                {
                    _values = new System.Collections.Generic.Dictionary<string, object>();
                }
            }
            else
            {
                _values = new System.Collections.Generic.Dictionary<string, object>();
            }
        }

        private void SaveToDisk()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(_values);
            System.IO.File.WriteAllText(_path, json);
        }

        // --- Theme persistence ---
        public ViewModels.AppTheme LoadTheme()
        {
            if (_values.TryGetValue("Theme", out var val)
                && val is System.Text.Json.JsonElement je
                && je.ValueKind == System.Text.Json.JsonValueKind.String
                && System.Enum.TryParse<ViewModels.AppTheme>(je.GetString(), out var mode))
            {
                return mode;
            }
            return ViewModels.AppTheme.Dark;
        }

        public void SaveTheme(ViewModels.AppTheme mode)
        {
            _values["Theme"] = mode.ToString();
            SaveToDisk();
        }

        // --- Launch-at-startup persistence ---
        public bool LoadLaunchAtStartup()
        {
            if (_values.TryGetValue("LaunchAtStartup", out var val) && val is bool b)
                return b;
            return false;
        }

        public void SaveLaunchAtStartup(bool enabled)
        {
            _values["LaunchAtStartup"] = enabled;
            SaveToDisk();
        }

        // --- Harmful threshold persistence ---
        public double LoadHarmfulThreshold()
        {
            if (_values.TryGetValue("HarmfulThreshold", out var val))
            {
                if (val is double d)
                    return d;
                if (val is System.Text.Json.JsonElement je && je.TryGetDouble(out var dVal))
                    return dVal;
            }
            return 75.0; // Default recommended threshold
        }

        public void SaveHarmfulThreshold(double dB)
        {
            _values["HarmfulThreshold"] = dB;
            SaveToDisk();
        }
    }
}
