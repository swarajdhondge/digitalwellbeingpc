using System;

namespace digital_wellbeing_app.Models
{
    // Summary per day
    public class ScreenTimePeriod
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }
        public string SessionDate { get; set; } = string.Empty; // "yyyy-MM-dd"
        public string SessionStartTime { get; set; } = string.Empty; // for compatibility, can ignore
        public string LastRecordedTime { get; set; } = string.Empty; // for compatibility, can ignore
        public int AccumulatedActiveSeconds { get; set; }
    }

    // Individual session per activity/unlock (useful for timeline, hourly, gaps)
    public class ScreenTimeSession
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }
        public string SessionDate { get; set; } = string.Empty; // "yyyy-MM-dd"
        public DateTime StartTime { get; set; }
        public int DurationSeconds { get; set; }
    }
}
