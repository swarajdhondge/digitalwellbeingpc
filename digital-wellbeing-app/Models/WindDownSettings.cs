using SQLite;

namespace digital_wellbeing_app.Models
{
    /// <summary>
    /// Settings model for Wind Down Mode functionality.
    /// Wind Down provides subtle end-of-day awareness without blocking anything.
    /// </summary>
    public class WindDownSettings
    {
        [PrimaryKey]
        public string Key { get; set; } = "WindDown";

        /// <summary>Whether Wind Down mode is enabled</summary>
        public bool IsEnabled { get; set; } = false;

        /// <summary>Start time for Wind Down (hours in 24h format, e.g., 21 = 9 PM)</summary>
        public int StartHour { get; set; } = 21;

        /// <summary>Start time minutes (0-59)</summary>
        public int StartMinute { get; set; } = 0;

        /// <summary>End time for Wind Down (hours in 24h format, e.g., 7 = 7 AM)</summary>
        public int EndHour { get; set; } = 7;

        /// <summary>End time minutes (0-59)</summary>
        public int EndMinute { get; set; } = 0;

        /// <summary>Whether to show a notification when Wind Down starts</summary>
        public bool ShowNotification { get; set; } = true;

        /// <summary>Whether to show the visual border overlay</summary>
        public bool ShowVisualCue { get; set; } = true;

        /// <summary>Visual cue style (0 = Amber glow, 1 = Purple/calm, 2 = Dim)</summary>
        public int VisualStyle { get; set; } = 0;

        /// <summary>Opacity of the visual cue (0.0 - 1.0, default 0.3)</summary>
        public double VisualOpacity { get; set; } = 0.3;
    }

    /// <summary>
    /// Settings keys for Wind Down mode persistence
    /// </summary>
    public static class WindDownKeys
    {
        public const string IsEnabled = "WindDown_Enabled";
        public const string StartHour = "WindDown_StartHour";
        public const string StartMinute = "WindDown_StartMinute";
        public const string EndHour = "WindDown_EndHour";
        public const string EndMinute = "WindDown_EndMinute";
        public const string ShowNotification = "WindDown_ShowNotification";
        public const string ShowVisualCue = "WindDown_ShowVisualCue";
        public const string VisualStyle = "WindDown_VisualStyle";
        public const string VisualOpacity = "WindDown_VisualOpacity";
    }

    /// <summary>
    /// Visual style options for Wind Down border effect
    /// </summary>
    public enum WindDownVisualStyle
    {
        /// <summary>Warm amber glow - suggests it's time to rest</summary>
        Amber = 0,

        /// <summary>Calm purple/lavender - gentle nighttime feeling</summary>
        Purple = 1,

        /// <summary>Subtle dim effect - minimal but noticeable</summary>
        Dim = 2
    }
}

