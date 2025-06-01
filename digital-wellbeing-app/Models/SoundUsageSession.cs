using System;
using SQLite;

namespace digital_wellbeing_app.Models
{
    public class SoundUsageSession
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        // Average volume scalar (0.0–1.0)
        public double AvgVolume { get; set; }

        // Estimated maximum SPL (in dB)
        public double EstimatedMaxSPL { get; set; }

        // Device name (e.g. "Realtek High Definition Audio")
        public string DeviceName { get; set; } = string.Empty;

        // Device type: "Headphones", "Speakers", etc.
        public string DeviceType { get; set; } = string.Empty;

        // True if any part of this session was above threshold
        public bool WasHarmful { get; set; }

        // Total duration during this session above threshold
        public TimeSpan HarmfulDuration { get; set; }
    }
}
