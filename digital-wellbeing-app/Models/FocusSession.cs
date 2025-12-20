using System;
using SQLite;

namespace digital_wellbeing_app.Models
{
    /// <summary>
    /// Enforcement level for Focus Mode
    /// </summary>
    public enum FocusEnforcementLevel
    {
        /// <summary>Show popup when opening distracting app (allow override)</summary>
        Warn = 0,
        /// <summary>Prevent launch during focus time</summary>
        Block = 1,
        /// <summary>Remove from taskbar/Start (softest) - Future feature</summary>
        Hide = 2
    }

    /// <summary>
    /// Model for tracking focus sessions
    /// </summary>
    [Table("FocusSession")]
    public class FocusSession
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>
        /// When the focus session started
        /// </summary>
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When the focus session ended (null if still active)
        /// </summary>
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Planned duration in minutes
        /// </summary>
        public int PlannedDurationMinutes { get; set; }

        /// <summary>
        /// The enforcement level used for this session
        /// </summary>
        public FocusEnforcementLevel EnforcementLevel { get; set; }

        /// <summary>
        /// Whether the session was completed (not cancelled early)
        /// </summary>
        public bool Completed { get; set; }

        /// <summary>
        /// Number of times user was warned about distracting apps
        /// </summary>
        public int DistractionWarnings { get; set; }

        /// <summary>
        /// Number of times user overrode a warning to continue using a distracting app
        /// </summary>
        public int DistractionOverrides { get; set; }

        /// <summary>
        /// Actual duration of the session
        /// </summary>
        [Ignore]
        public TimeSpan Duration => EndTime.HasValue 
            ? EndTime.Value - StartTime 
            : DateTime.Now - StartTime;

        /// <summary>
        /// Whether this session is currently active
        /// </summary>
        [Ignore]
        public bool IsActive => !EndTime.HasValue;

        /// <summary>
        /// Date key for queries (yyyy-MM-dd format)
        /// </summary>
        public string SessionDate { get; set; } = DateTime.Now.ToString("yyyy-MM-dd");
    }

    /// <summary>
    /// Settings keys for Focus Sessions
    /// </summary>
    public static class FocusSessionKeys
    {
        public const string EnforcementLevel = "Focus_EnforcementLevel";
        public const string DefaultDurationMinutes = "Focus_DefaultDuration";
        public const string BlockEntertainment = "Focus_BlockEntertainment";
        public const string AllowWorkApps = "Focus_AllowWorkApps";
    }
}

