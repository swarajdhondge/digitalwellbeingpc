using SQLite;

namespace digital_wellbeing_app.Models
{
    /// <summary>
    /// Generic key-value settings storage for user preferences.
    /// </summary>
    public class UserSettings
    {
        [PrimaryKey]
        public string Key { get; set; } = string.Empty;
        
        public string Value { get; set; } = string.Empty;
    }
    
    /// <summary>
    /// Well-known settings keys
    /// </summary>
    public static class SettingsKeys
    {
        /// <summary>Daily screen time goal in minutes (null = no limit)</summary>
        public const string ScreenTimeGoal = "ScreenTimeGoal";
        
        /// <summary>Whether goal notifications are enabled</summary>
        public const string GoalNotificationsEnabled = "GoalNotificationsEnabled";

        /// <summary>Minutes between goal reminders (0 = once a day)</summary>
        public const string GoalRemindEveryMinutes = "GoalRemindEveryMinutes";

        /// <summary>When the goal reminder was last shown (round-trip date/time)</summary>
        public const string GoalLastReminder = "GoalLastReminder";
    }
}

