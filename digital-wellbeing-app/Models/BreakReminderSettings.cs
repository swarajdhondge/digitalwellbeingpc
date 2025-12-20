using SQLite;

namespace digital_wellbeing_app.Models
{
    /// <summary>
    /// Settings model for break reminder functionality (20-20-20 rule)
    /// </summary>
    public class BreakReminderSettings
    {
        [PrimaryKey]
        public string Key { get; set; } = "BreakReminder";
        
        /// <summary>Whether break reminders are enabled</summary>
        public bool IsEnabled { get; set; } = false;
        
        /// <summary>Interval between break reminders in minutes</summary>
        public int IntervalMinutes { get; set; } = 20;
        
        /// <summary>Whether to play a sound when break is due</summary>
        public bool SoundEnabled { get; set; } = true;
    }
    
    /// <summary>
    /// Extended settings keys for break reminders
    /// </summary>
    public static class BreakReminderKeys
    {
        public const string IsEnabled = "BreakReminder_Enabled";
        public const string IntervalMinutes = "BreakReminder_Interval";
        public const string SoundEnabled = "BreakReminder_Sound";
    }
}

