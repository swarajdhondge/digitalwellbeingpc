namespace digital_wellbeing_app.Models
{
    public class ScreenTimeSession
    {
        [SQLite.PrimaryKey, SQLite.AutoIncrement]
        public int Id { get; set; }

        public string SessionDate { get; set; } = string.Empty;
        public string SessionStartTime { get; set; } = string.Empty;
        public string LastRecordedTime { get; set; } = string.Empty;
        public int AccumulatedActiveSeconds { get; set; }
    }
}
