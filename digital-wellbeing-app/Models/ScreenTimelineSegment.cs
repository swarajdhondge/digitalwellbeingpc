namespace digital_wellbeing_app.Models
{
    public class ScreenTimelineSegment
    {
        public double StartPercent { get; set; } // fraction of 24hr day (0.0–1.0)
        public double WidthPercent { get; set; } // fraction of 24hr day (0.0–1.0)
    }
}
