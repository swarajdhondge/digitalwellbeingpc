using System;
using SQLite;

namespace digital_wellbeing_app.Models
{
    /// <summary>
    /// A per-app daily time cap and/or time-of-day restriction, enforced independently of Focus
    /// Sessions (i.e. active all the time, not just while a session is running).
    /// </summary>
    [Table("AppLimit")]
    public class AppLimit
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        /// <summary>Canonical app identity - see <see cref="Helpers.AppIdentity.NormalizeKey(string?)"/>.</summary>
        [Indexed]
        public string AppIdentifier { get; set; } = string.Empty;

        public string AppName { get; set; } = string.Empty;

        /// <summary>Full path to the executable (for icon extraction).</summary>
        public string ExecutablePath { get; set; } = string.Empty;

        /// <summary>Master switch - a disabled limit is kept (not deleted) but never enforced.</summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>Daily usage cap in minutes. 0 = no cap (schedule-only limit).</summary>
        public int DailyLimitMinutes { get; set; } = 0;

        /// <summary>Whether the time-of-day restriction below is active.</summary>
        public bool ScheduleEnabled { get; set; } = false;

        public int ScheduleStartHour { get; set; } = 9;
        public int ScheduleStartMinute { get; set; } = 0;
        public int ScheduleEndHour { get; set; } = 17;
        public int ScheduleEndMinute { get; set; } = 0;

        /// <summary>What happens when the limit is hit. Reuses Focus Sessions' enum rather than
        /// forking a parallel one - the two features react to their own trigger the same way.</summary>
        public FocusEnforcementLevel EnforcementLevel { get; set; } = FocusEnforcementLevel.Warn;

        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
