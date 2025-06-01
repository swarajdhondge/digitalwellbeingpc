using System;
using System.Collections.ObjectModel;
using System.Linq;
using digital_wellbeing_app.Models;
using digital_wellbeing_app.Services;

namespace digital_wellbeing_app.ViewModels
{
    public class SoundTimelineViewModel
    {
        public ObservableCollection<TimelineBar> Bars { get; }
            = new ObservableCollection<TimelineBar>();

        public ObservableCollection<SoundUsageSession> Sessions { get; }
            = new ObservableCollection<SoundUsageSession>();

        public TimeSpan TotalListeningTime { get; private set; }
        public TimeSpan TotalHarmfulTime { get; private set; }

        public SoundTimelineViewModel()
        {
            RefreshData();
        }

        public void RefreshData()
        {
            Bars.Clear();
            Sessions.Clear();
            TotalListeningTime = TimeSpan.Zero;
            TotalHarmfulTime = TimeSpan.Zero;

            // 1) Fetch raw sessions from database, ordered by start time
            var rawSessions = DatabaseService
                              .GetSoundSessionsForDate(DateTime.Now)
                              .OrderBy(s => s.StartTime)
                              .ToList();

            foreach (var s in rawSessions)
            {
                Sessions.Add(s);

                var duration = s.EndTime - s.StartTime;
                TotalListeningTime += duration;
                TotalHarmfulTime += s.HarmfulDuration;

                // Build a “bar” for timeline
                var dayStart = DateTime.Now.Date;
                double secondsInDay = TimeSpan.FromHours(24).TotalSeconds;

                var startOff = (s.StartTime - dayStart).TotalSeconds;
                var endOff = (s.EndTime - dayStart).TotalSeconds;

                startOff = Math.Max(0, Math.Min(secondsInDay, startOff));
                endOff = Math.Max(0, Math.Min(secondsInDay, endOff));

                double startFrac = startOff / secondsInDay;
                double endFrac = endOff / secondsInDay;

                double harmFrac = 0;
                if (s.HarmfulDuration.TotalSeconds > 0 && duration.TotalSeconds > 0)
                {
                    harmFrac = Math.Min(1.0, s.HarmfulDuration.TotalSeconds / duration.TotalSeconds);
                }

                Bars.Add(new TimelineBar
                {
                    StartFrac = startFrac,
                    EndFrac = endFrac,
                    HarmfulFrac = harmFrac,
                    DeviceName = s.DeviceName,
                    SessionLabel = $"{s.StartTime:HH:mm}–{s.EndTime:HH:mm}"
                });
            }
        }
    }

    public class TimelineBar
    {
        public double StartFrac { get; set; }
        public double EndFrac { get; set; }
        public double HarmfulFrac { get; set; }
        public string DeviceName { get; set; } = "";
        public string SessionLabel { get; set; } = "";
    }
}
