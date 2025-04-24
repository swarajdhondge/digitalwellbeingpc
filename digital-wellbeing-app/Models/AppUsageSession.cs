using System;
using SQLite;

namespace digital_wellbeing_app.Models
{
    [Table("AppUsageSession")]
    public class AppUsageSession
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public string AppName { get; set; } = string.Empty;
        public string ExecutablePath { get; set; } = string.Empty;
        public string? WindowTitle { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        [Ignore]
        public TimeSpan Duration => EndTime - StartTime;
    }
}
