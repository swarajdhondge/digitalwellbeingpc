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

        // --- Break reminder persistence ---
        public bool LoadBreakReminderEnabled()
        {
            if (_values.TryGetValue(Models.BreakReminderKeys.IsEnabled, out var val))
            {
                if (val is bool b)
                    return b;
                if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.True)
                    return true;
                if (val is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.False)
                    return false;
            }
            return false;
        }

        public void SaveBreakReminderEnabled(bool enabled)
        {
            _values[Models.BreakReminderKeys.IsEnabled] = enabled;
            SaveToDisk();
        }

        public int LoadBreakReminderInterval()
        {
            if (_values.TryGetValue(Models.BreakReminderKeys.IntervalMinutes, out var val))
            {
                if (val is int i)
                    return i;
                if (val is System.Text.Json.JsonElement je && je.TryGetInt32(out var intVal))
                    return intVal;
            }
            return 20; // Default: 20 minutes
        }

        public void SaveBreakReminderInterval(int minutes)
        {
            _values[Models.BreakReminderKeys.IntervalMinutes] = minutes;
            SaveToDisk();
        }

        public bool LoadBreakReminderSound()
        {
            if (_values.TryGetValue(Models.BreakReminderKeys.SoundEnabled, out var val))
            {
                if (val is bool b)
                    return b;
                if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.True)
                    return true;
                if (val is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.False)
                    return false;
            }
            return true; // Default: sound enabled
        }

        public void SaveBreakReminderSound(bool enabled)
        {
            _values[Models.BreakReminderKeys.SoundEnabled] = enabled;
            SaveToDisk();
        }

        // --- Internal helper for BreakReminderService ---
        internal System.Collections.Generic.Dictionary<string, object> GetAllSettings()
        {
            return new System.Collections.Generic.Dictionary<string, object>(_values);
        }

        // --- Focus Session persistence ---
        public Models.FocusEnforcementLevel LoadFocusEnforcementLevel()
        {
            if (_values.TryGetValue(Models.FocusSessionKeys.EnforcementLevel, out var val))
            {
                if (val is int i)
                    return (Models.FocusEnforcementLevel)i;
                if (val is System.Text.Json.JsonElement je && je.TryGetInt32(out var intVal))
                    return (Models.FocusEnforcementLevel)intVal;
            }
            return Models.FocusEnforcementLevel.Warn; // Default: Warn
        }

        public void SaveFocusEnforcementLevel(Models.FocusEnforcementLevel level)
        {
            _values[Models.FocusSessionKeys.EnforcementLevel] = (int)level;
            SaveToDisk();
        }

        public int LoadFocusDefaultDuration()
        {
            if (_values.TryGetValue(Models.FocusSessionKeys.DefaultDurationMinutes, out var val))
            {
                if (val is int i)
                    return i;
                if (val is System.Text.Json.JsonElement je && je.TryGetInt32(out var intVal))
                    return intVal;
            }
            return 25; // Default: 25 minutes (Pomodoro)
        }

        public void SaveFocusDefaultDuration(int minutes)
        {
            _values[Models.FocusSessionKeys.DefaultDurationMinutes] = minutes;
            SaveToDisk();
        }

        public bool LoadFocusBlockEntertainment()
        {
            if (_values.TryGetValue(Models.FocusSessionKeys.BlockEntertainment, out var val))
            {
                if (val is bool b)
                    return b;
                if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.True)
                    return true;
                if (val is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.False)
                    return false;
            }
            return true; // Default: block entertainment apps
        }

        public void SaveFocusBlockEntertainment(bool block)
        {
            _values[Models.FocusSessionKeys.BlockEntertainment] = block;
            SaveToDisk();
        }

        public bool LoadFocusAllowWorkApps()
        {
            if (_values.TryGetValue(Models.FocusSessionKeys.AllowWorkApps, out var val))
            {
                if (val is bool b)
                    return b;
                if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.True)
                    return true;
                if (val is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.False)
                    return false;
            }
            return true; // Default: allow work apps
        }

        public void SaveFocusAllowWorkApps(bool allow)
        {
            _values[Models.FocusSessionKeys.AllowWorkApps] = allow;
            SaveToDisk();
        }

        public bool LoadFocusSoundOnComplete()
        {
            if (_values.TryGetValue("Focus_SoundOnComplete", out var val))
            {
                if (val is bool b)
                    return b;
                if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.True)
                    return true;
                if (val is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.False)
                    return false;
            }
            return true; // Default: play sound on complete
        }

        public void SaveFocusSoundOnComplete(bool sound)
        {
            _values["Focus_SoundOnComplete"] = sound;
            SaveToDisk();
        }
    }
}
