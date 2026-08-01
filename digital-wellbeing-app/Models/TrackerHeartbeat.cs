using System;
using SQLite;

namespace digital_wellbeing_app.Models
{
    /// <summary>
    /// Last-known-alive marker for a tracker, written on a throttled cadence from that tracker's
    /// own existing periodic callback. Diagnostic metadata, not usage history or a user
    /// preference - like AppCategory/AppLimit, excluded from DeleteAllData()/PurgeDataOlderThan()/
    /// DeleteDataInRange().
    /// </summary>
    [Table("TrackerHeartbeat")]
    public class TrackerHeartbeat
    {
        /// <summary>e.g. "ScreenTimeTracker", "AppUsageTracker", "SoundExposureManager".</summary>
        [PrimaryKey]
        public string TrackerName { get; set; } = string.Empty;

        public DateTime LastSeenUtc { get; set; }

        /// <summary>Total heartbeats recorded since this row was first created (not since app start).</summary>
        public long TickCount { get; set; }

        /// <summary>Most recent error message the tracker reported, if any. Null when healthy.</summary>
        public string? LastError { get; set; }
    }
}
