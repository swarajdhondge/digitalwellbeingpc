using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace digital_wellbeing_app.Services
{
    public class SettingsService
    {
        private const string FolderName = "Pulse";
        private const string FileName = "settings.json";
        private readonly string _path;
        private readonly System.Collections.Generic.Dictionary<string, object> _values;
        private static bool _aclApplied = false;

        public SettingsService()
        {
            var folder = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
                FolderName);

            bool created = !System.IO.Directory.Exists(folder);
            System.IO.Directory.CreateDirectory(folder);

            // Restrict directory ACL on first creation
            if (created && !_aclApplied)
            {
                RestrictDirectoryToCurrentUser(folder);
                _aclApplied = true;
            }

            _path = System.IO.Path.Combine(folder, FileName);
            _values = LoadFromFile(_path);

            // If primary file was corrupt/missing, try backup
            if (_values.Count == 0 && System.IO.File.Exists(_path + ".bak"))
            {
                _values = LoadFromFile(_path + ".bak");
                if (_values.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine("[Settings] Recovered from backup file");
                }
            }
        }

        private static System.Collections.Generic.Dictionary<string, object> LoadFromFile(string path)
        {
            if (!System.IO.File.Exists(path))
                return new System.Collections.Generic.Dictionary<string, object>();

            try
            {
                var json = System.IO.File.ReadAllText(path);
                return System.Text.Json.JsonSerializer
                    .Deserialize<System.Collections.Generic.Dictionary<string, object>>(json)
                    ?? new System.Collections.Generic.Dictionary<string, object>();
            }
            catch
            {
                return new System.Collections.Generic.Dictionary<string, object>();
            }
        }

        private void SaveToDisk()
        {
            try
            {
                // Create backup of current file before writing
                if (System.IO.File.Exists(_path))
                {
                    System.IO.File.Copy(_path, _path + ".bak", overwrite: true);
                }

                var json = System.Text.Json.JsonSerializer.Serialize(_values);
                System.IO.File.WriteAllText(_path, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] SaveToDisk error: {ex.Message}");
            }
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

        internal System.Collections.Generic.Dictionary<string, object> GetAllSettings()
        {
            return new System.Collections.Generic.Dictionary<string, object>(_values);
        }

        internal bool LoadBoolSetting(string key, bool defaultValue)
        {
            try
            {
                if (_values.TryGetValue(key, out var value))
                {
                    if (value is bool b) return b;
                    if (value is System.Text.Json.JsonElement je)
                    {
                        if (je.ValueKind == System.Text.Json.JsonValueKind.True) return true;
                        if (je.ValueKind == System.Text.Json.JsonValueKind.False) return false;
                    }
                }
            }
            catch { }
            return defaultValue;
        }

        internal int LoadIntSetting(string key, int defaultValue)
        {
            try
            {
                if (_values.TryGetValue(key, out var value))
                {
                    if (value is int i) return i;
                    if (value is System.Text.Json.JsonElement je && je.TryGetInt32(out var intVal))
                        return intVal;
                }
            }
            catch { }
            return defaultValue;
        }

        internal double LoadDoubleSetting(string key, double defaultValue)
        {
            try
            {
                if (_values.TryGetValue(key, out var value))
                {
                    if (value is double d) return d;
                    if (value is System.Text.Json.JsonElement je && je.TryGetDouble(out var dVal))
                        return dVal;
                }
            }
            catch { }
            return defaultValue;
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

        // --- First-run flag ---
        public bool LoadFirstRunCompleted()
        {
            if (_values.TryGetValue("FirstRunCompleted", out var val))
            {
                if (val is bool b) return b;
                if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.True)
                    return true;
            }
            return false;
        }

        public void SaveFirstRunCompleted(bool completed)
        {
            _values["FirstRunCompleted"] = completed;
            SaveToDisk();
        }

        // --- Wind Down persistence ---
        public bool LoadWindDownEnabled()
        {
            if (_values.TryGetValue(Models.WindDownKeys.IsEnabled, out var val))
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

        public void SaveWindDownEnabled(bool enabled)
        {
            _values[Models.WindDownKeys.IsEnabled] = enabled;
            SaveToDisk();
        }

        public int LoadWindDownStartHour()
        {
            if (_values.TryGetValue(Models.WindDownKeys.StartHour, out var val))
            {
                if (val is int i)
                    return i;
                if (val is System.Text.Json.JsonElement je && je.TryGetInt32(out var intVal))
                    return intVal;
            }
            return 21; // Default: 9 PM
        }

        public int LoadWindDownStartMinute()
        {
            if (_values.TryGetValue(Models.WindDownKeys.StartMinute, out var val))
            {
                if (val is int i)
                    return i;
                if (val is System.Text.Json.JsonElement je && je.TryGetInt32(out var intVal))
                    return intVal;
            }
            return 0;
        }

        public void SaveWindDownStartTime(int hour, int minute)
        {
            _values[Models.WindDownKeys.StartHour] = hour;
            _values[Models.WindDownKeys.StartMinute] = minute;
            SaveToDisk();
        }

        public int LoadWindDownEndHour()
        {
            if (_values.TryGetValue(Models.WindDownKeys.EndHour, out var val))
            {
                if (val is int i)
                    return i;
                if (val is System.Text.Json.JsonElement je && je.TryGetInt32(out var intVal))
                    return intVal;
            }
            return 7; // Default: 7 AM
        }

        public int LoadWindDownEndMinute()
        {
            if (_values.TryGetValue(Models.WindDownKeys.EndMinute, out var val))
            {
                if (val is int i)
                    return i;
                if (val is System.Text.Json.JsonElement je && je.TryGetInt32(out var intVal))
                    return intVal;
            }
            return 0;
        }

        public void SaveWindDownEndTime(int hour, int minute)
        {
            _values[Models.WindDownKeys.EndHour] = hour;
            _values[Models.WindDownKeys.EndMinute] = minute;
            SaveToDisk();
        }

        public bool LoadWindDownShowNotification()
        {
            if (_values.TryGetValue(Models.WindDownKeys.ShowNotification, out var val))
            {
                if (val is bool b)
                    return b;
                if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.True)
                    return true;
                if (val is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.False)
                    return false;
            }
            return true; // Default: show notification
        }

        public void SaveWindDownShowNotification(bool show)
        {
            _values[Models.WindDownKeys.ShowNotification] = show;
            SaveToDisk();
        }

        public bool LoadWindDownShowVisualCue()
        {
            if (_values.TryGetValue(Models.WindDownKeys.ShowVisualCue, out var val))
            {
                if (val is bool b)
                    return b;
                if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.True)
                    return true;
                if (val is System.Text.Json.JsonElement je2 && je2.ValueKind == System.Text.Json.JsonValueKind.False)
                    return false;
            }
            return true; // Default: show visual cue
        }

        public void SaveWindDownShowVisualCue(bool show)
        {
            _values[Models.WindDownKeys.ShowVisualCue] = show;
            SaveToDisk();
        }

        public int LoadWindDownVisualStyle()
        {
            if (_values.TryGetValue(Models.WindDownKeys.VisualStyle, out var val))
            {
                if (val is int i)
                    return i;
                if (val is System.Text.Json.JsonElement je && je.TryGetInt32(out var intVal))
                    return intVal;
            }
            return 0; // Default: Amber
        }

        public void SaveWindDownVisualStyle(int style)
        {
            _values[Models.WindDownKeys.VisualStyle] = style;
            SaveToDisk();
        }

        public double LoadWindDownVisualOpacity()
        {
            if (_values.TryGetValue(Models.WindDownKeys.VisualOpacity, out var val))
            {
                if (val is double d)
                    return d;
                if (val is System.Text.Json.JsonElement je && je.TryGetDouble(out var dVal))
                    return dVal;
            }
            return 0.3; // Default: 30% opacity
        }

        public void SaveWindDownVisualOpacity(double opacity)
        {
            _values[Models.WindDownKeys.VisualOpacity] = opacity;
            SaveToDisk();
        }

        // --- Window state persistence ---
        public void SaveWindowState(double left, double top, double width, double height, bool isMaximized)
        {
            _values["WindowLeft"] = left;
            _values["WindowTop"] = top;
            _values["WindowWidth"] = width;
            _values["WindowHeight"] = height;
            _values["WindowMaximized"] = isMaximized;
            SaveToDisk();
        }

        /// <summary>
        /// Load saved window position/size. Returns null if no saved state exists.
        /// </summary>
        public (double Left, double Top, double Width, double Height, bool IsMaximized)? LoadWindowState()
        {
            if (!_values.ContainsKey("WindowWidth"))
                return null;

            double GetDouble(string key, double fallback)
            {
                if (_values.TryGetValue(key, out var val))
                {
                    if (val is double d) return d;
                    if (val is System.Text.Json.JsonElement je && je.TryGetDouble(out var dVal)) return dVal;
                }
                return fallback;
            }

            bool GetBool(string key)
            {
                if (_values.TryGetValue(key, out var val))
                {
                    if (val is bool b) return b;
                    if (val is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.True) return true;
                }
                return false;
            }

            return (
                GetDouble("WindowLeft", 100),
                GetDouble("WindowTop", 100),
                GetDouble("WindowWidth", 1100),
                GetDouble("WindowHeight", 750),
                GetBool("WindowMaximized")
            );
        }

        /// <summary>
        /// Restricts a directory's ACL to the current user only.
        /// </summary>
        private static void RestrictDirectoryToCurrentUser(string directoryPath)
        {
            try
            {
                var dirInfo = new System.IO.DirectoryInfo(directoryPath);
                var security = dirInfo.GetAccessControl();

                security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

                foreach (System.Security.AccessControl.FileSystemAccessRule rule in
                    security.GetAccessRules(true, true, typeof(System.Security.Principal.SecurityIdentifier)))
                {
                    security.RemoveAccessRule(rule);
                }

                var currentUser = System.Security.Principal.WindowsIdentity.GetCurrent().User;
                if (currentUser != null)
                {
                    security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(
                        currentUser,
                        System.Security.AccessControl.FileSystemRights.FullControl,
                        System.Security.AccessControl.InheritanceFlags.ContainerInherit |
                        System.Security.AccessControl.InheritanceFlags.ObjectInherit,
                        System.Security.AccessControl.PropagationFlags.None,
                        System.Security.AccessControl.AccessControlType.Allow));
                }

                dirInfo.SetAccessControl(security);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Settings] ACL restriction failed: {ex.Message}");
            }
        }
    }
}
