using SQLite;

namespace digital_wellbeing_app.Models
{
    /// <summary>
    /// Settings model for Focus Sessions functionality
    /// </summary>
    public class FocusSessionSettings
    {
        [PrimaryKey]
        public string Key { get; set; } = "FocusSession";

        /// <summary>Default duration for focus sessions in minutes</summary>
        public int DefaultDurationMinutes { get; set; } = 25;

        /// <summary>Enforcement level (Warn, Block, Hide)</summary>
        public FocusEnforcementLevel EnforcementLevel { get; set; } = FocusEnforcementLevel.Warn;

        /// <summary>Whether to block entertainment apps during focus</summary>
        public bool BlockEntertainment { get; set; } = true;

        /// <summary>Whether work apps are always allowed</summary>
        public bool AllowWorkApps { get; set; } = true;

        /// <summary>Whether to play sound when focus session ends</summary>
        public bool SoundOnComplete { get; set; } = true;

        /// <summary>Whether to show notification when focus ends</summary>
        public bool NotifyOnComplete { get; set; } = true;
    }
}

